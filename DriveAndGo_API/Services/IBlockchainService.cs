using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DriveAndGo_API.Services
{
    public interface IBlockchainService
    {
        Task<string> AppendBlockAsync(int rentalId, string actionType, object contractDetails);
        Task<BlockchainVerificationResult> VerifyRentalChainAsync(int rentalId);
        Task<List<BlockchainBlockDto>> GetRentalBlocksAsync(int rentalId);
    }

    public class BlockchainBlockDto
    {
        public int BlockIndex { get; set; }
        public int? RentalId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string BlockHash { get; set; } = string.Empty;
        public string PreviousHash { get; set; } = string.Empty;
        public string ContractDataJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class BlockchainVerificationResult
    {
        public int RentalId { get; set; }
        public int TotalBlocks { get; set; }
        public bool IsValid { get; set; }
        public string? TamperedAt { get; set; }
        public string LatestHash { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<BlockchainBlockDto> Blocks { get; set; } = new();
    }
}
