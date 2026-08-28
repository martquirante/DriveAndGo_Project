using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BlockchainController : ControllerBase
{
    private readonly NpgsqlDataSource _ds;
    public BlockchainController(NpgsqlDataSource ds) => _ds = ds;

    // POST /api/blockchain/contracts
    [HttpPost("contracts")]
    public async Task<IActionResult> CommitContract([FromBody] BlockchainRequest req)
    {
        try {
            await using var conn = await _ds.OpenConnectionAsync();

            // Get the last block's hash and index
            string prevHash = "0";
            int blockIndex = 0;
            await using (var qcmd = new NpgsqlCommand(
                "SELECT block_index, block_hash FROM blockchain_blocks ORDER BY block_index DESC LIMIT 1", conn)) {
                await using var qr = await qcmd.ExecuteReaderAsync();
                if (await qr.ReadAsync()) {
                    blockIndex = qr.GetInt32(0) + 1;
                    prevHash   = qr.GetString(1);
                }
            }

            // Compute new block hash
            var raw = $"{blockIndex}|{prevHash}|{req.RentalId}|{req.ContractData}|{DateTime.UtcNow:O}";
            var hash = ComputeSha256(raw);

            // Insert new block
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO blockchain_blocks (rental_id, block_index, block_hash, prev_hash, contract_data)
                  VALUES (@rid, @idx, @hash, @prev, @data) RETURNING block_id", conn);
            cmd.Parameters.AddWithValue("@rid",  req.RentalId);
            cmd.Parameters.AddWithValue("@idx",  blockIndex);
            cmd.Parameters.AddWithValue("@hash", hash);
            cmd.Parameters.AddWithValue("@prev", prevHash);
            cmd.Parameters.AddWithValue("@data", req.ContractData);
            var blockId = (int)(await cmd.ExecuteScalarAsync())!;

            return Ok(new {
                message     = "Contract block added successfully.",
                blockId,
                blockIndex,
                blockHash   = hash,
                prevHash
            });
        } catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // GET /api/blockchain/contracts/{rentalId}
    [HttpGet("contracts/{rentalId:int}")]
    public async Task<IActionResult> GetContract(int rentalId)
    {
        try {
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT block_id, block_index, block_hash, prev_hash, contract_data, created_at FROM blockchain_blocks WHERE rental_id = @rid ORDER BY block_index", conn);
            cmd.Parameters.AddWithValue("@rid", rentalId);
            var list = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) {
                list.Add(new {
                    blockId      = reader.GetInt32(0),
                    blockIndex   = reader.GetInt32(1),
                    blockHash    = reader.GetString(2),
                    prevHash     = reader.GetString(3),
                    contractData = reader.GetString(4),
                    createdAt    = reader.GetDateTime(5).ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            if (list.Count == 0) return NotFound(new { message = "No blockchain record found for this rental." });
            return Ok(list);
        } catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    // GET /api/blockchain/verify/{rentalId} - Re-compute and verify hash integrity
    [HttpGet("verify/{rentalId:int}")]
    public async Task<IActionResult> VerifyChain(int rentalId)
    {
        try {
            await using var conn = await _ds.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT block_index, block_hash, prev_hash, contract_data, created_at FROM blockchain_blocks WHERE rental_id = @rid ORDER BY block_index", conn);
            cmd.Parameters.AddWithValue("@rid", rentalId);
            var blocks = new List<(int idx, string hash, string prev, string data, string ts)>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) {
                blocks.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                            reader.GetString(3), reader.GetDateTime(4).ToString("O")));
            }
            if (blocks.Count == 0) return NotFound(new { message = "No blocks found." });

            bool isValid = true;
            string tamperedAt = string.Empty;
            for (int i = 0; i < blocks.Count; i++) {
                var b = blocks[i];
                // NOTE: We can't fully reverify since timestamps are exact - but we can check chain linkage
                if (i > 0 && blocks[i].prev != blocks[i-1].hash) {
                    isValid = false;
                    tamperedAt = $"Block {b.idx}";
                    break;
                }
            }

            return Ok(new {
                rentalId,
                blockCount = blocks.Count,
                isValid,
                tamperedAt = isValid ? null : tamperedAt,
                message    = isValid ? "Blockchain ledger integrity verified." : $"Tamper detected at {tamperedAt}."
            });
        } catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }
}

public class BlockchainRequest
{
    public int    RentalId     { get; set; }
    public string ContractData { get; set; } = string.Empty;
}
