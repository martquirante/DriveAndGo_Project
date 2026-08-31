using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DriveAndGo_API.Services
{
    public class BlockchainService : IBlockchainService
    {
        private readonly NpgsqlDataSource _ds;
        private readonly ILogger<BlockchainService> _logger;
        private static readonly SemaphoreSlim _chainLock = new(1, 1);

        public BlockchainService(NpgsqlDataSource ds, ILogger<BlockchainService> logger)
        {
            _ds = ds;
            _logger = logger;
        }

        public async Task<string> AppendBlockAsync(int rentalId, string actionType, object contractDetails)
        {
            await _chainLock.WaitAsync();
            try
            {
                var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
                string contractJson = contractDetails is string s ? s : JsonSerializer.Serialize(contractDetails, jsonOptions);

                await using var conn = await _ds.OpenConnectionAsync();

                // 1. Fetch latest block to link previous_hash
                string previousHash = "0000000000000000000000000000000000000000000000000000000000000000";
                int nextIndex = 1;

                await using (var qcmd = new NpgsqlCommand(
                    "SELECT block_index, block_hash FROM blockchain_ledger ORDER BY block_index DESC LIMIT 1", conn))
                {
                    await using var reader = await qcmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        nextIndex = reader.GetInt32(0) + 1;
                        previousHash = reader.GetString(1);
                    }
                }

                var timestampUtc = DateTime.UtcNow;
                string timestampStr = timestampUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                // 2. Build canonical string for SHA-256 hashing
                string canonicalPayload = $"{nextIndex}|{previousHash}|{rentalId}|{actionType}|{contractJson}|{timestampStr}";
                string blockHash = ComputeSha256(canonicalPayload);

                // 3. Persist new block to ledger
                await using (var insertCmd = new NpgsqlCommand(@"
                    INSERT INTO blockchain_ledger (rental_id, action_type, block_hash, previous_hash, contract_data, created_at)
                    VALUES (@rid, @act, @hash, @prev, @data::jsonb, @created)
                    RETURNING block_index;", conn))
                {
                    insertCmd.Parameters.AddWithValue("@rid", rentalId);
                    insertCmd.Parameters.AddWithValue("@act", string.IsNullOrWhiteSpace(actionType) ? "CONTRACT_SEALED" : actionType);
                    insertCmd.Parameters.AddWithValue("@hash", blockHash);
                    insertCmd.Parameters.AddWithValue("@prev", previousHash);
                    insertCmd.Parameters.AddWithValue("@data", contractJson);
                    insertCmd.Parameters.AddWithValue("@created", timestampUtc);

                    var actualIndex = await insertCmd.ExecuteScalarAsync();
                    _logger.LogInformation("Blockchain block #{Index} sealed for rental #{RentalId} with hash {Hash}", actualIndex, rentalId, blockHash);
                }

                // 4. Update rentals table with the sealed certificate hash
                await using (var updateRentalCmd = new NpgsqlCommand(
                    "UPDATE rentals SET blockchain_hash = @hash WHERE rental_id = @rid", conn))
                {
                    updateRentalCmd.Parameters.AddWithValue("@hash", blockHash);
                    updateRentalCmd.Parameters.AddWithValue("@rid", rentalId);
                    await updateRentalCmd.ExecuteNonQueryAsync();
                }

                return blockHash;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seal blockchain block for rental #{RentalId}", rentalId);
                throw;
            }
            finally
            {
                _chainLock.Release();
            }
        }

        public async Task<List<BlockchainBlockDto>> GetRentalBlocksAsync(int rentalId)
        {
            var blocks = new List<BlockchainBlockDto>();
            await using var conn = await _ds.OpenConnectionAsync();

            await using var cmd = new NpgsqlCommand(@"
                SELECT block_index, rental_id, action_type, block_hash, previous_hash, contract_data::text, created_at
                FROM blockchain_ledger
                WHERE rental_id = @rid
                ORDER BY block_index ASC", conn);
            cmd.Parameters.AddWithValue("@rid", rentalId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                blocks.Add(new BlockchainBlockDto
                {
                    BlockIndex = reader.GetInt32(0),
                    RentalId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    ActionType = reader.IsDBNull(2) ? "CONTRACT_SEALED" : reader.GetString(2),
                    BlockHash = reader.GetString(3),
                    PreviousHash = reader.GetString(4),
                    ContractDataJson = reader.GetString(5),
                    CreatedAt = reader.GetDateTime(6)
                });
            }

            return blocks;
        }

        public async Task<BlockchainVerificationResult> VerifyRentalChainAsync(int rentalId)
        {
            var rentalBlocks = await GetRentalBlocksAsync(rentalId);
            if (rentalBlocks.Count == 0)
            {
                return new BlockchainVerificationResult
                {
                    RentalId = rentalId,
                    TotalBlocks = 0,
                    IsValid = false,
                    Message = "No cryptographic blocks found for this agreement.",
                    Blocks = rentalBlocks
                };
            }

            await using var conn = await _ds.OpenConnectionAsync();

            bool isValid = true;
            string? tamperedAt = null;

            for (int i = 0; i < rentalBlocks.Count; i++)
            {
                var currentBlock = rentalBlocks[i];

                // Verify that previous_hash links correctly to a preceding block in the ledger
                if (currentBlock.BlockIndex > 1)
                {
                    await using var checkCmd = new NpgsqlCommand(
                        "SELECT block_hash FROM blockchain_ledger WHERE block_index = @prevIdx", conn);
                    checkCmd.Parameters.AddWithValue("@prevIdx", currentBlock.BlockIndex - 1);
                    var prevHashDb = (await checkCmd.ExecuteScalarAsync())?.ToString();

                    if (prevHashDb != null && !string.Equals(prevHashDb, currentBlock.PreviousHash, StringComparison.OrdinalIgnoreCase))
                    {
                        isValid = false;
                        tamperedAt = $"Block #{currentBlock.BlockIndex} (Invalid previous hash linkage)";
                        break;
                    }
                }
            }

            string latestHash = rentalBlocks[^1].BlockHash;

            return new BlockchainVerificationResult
            {
                RentalId = rentalId,
                TotalBlocks = rentalBlocks.Count,
                IsValid = isValid,
                TamperedAt = tamperedAt,
                LatestHash = latestHash,
                Message = isValid
                    ? "Cryptographic chain verified. All digital contract stamps are mathematically intact and immutable."
                    : $"Tampering detected: {tamperedAt}",
                Blocks = rentalBlocks
            };
        }

        private static string ComputeSha256(string raw)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
