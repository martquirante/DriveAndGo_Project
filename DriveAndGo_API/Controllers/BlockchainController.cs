using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DriveAndGo_API.Services;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BlockchainController : ControllerBase
    {
        private readonly IBlockchainService _blockchainService;

        public BlockchainController(IBlockchainService blockchainService)
        {
            _blockchainService = blockchainService;
        }

        // POST /api/blockchain/contracts
        [HttpPost("contracts")]
        public async Task<IActionResult> CommitContract([FromBody] BlockchainRequest req)
        {
            try
            {
                if (req.RentalId <= 0)
                {
                    return BadRequest(new { message = "Invalid Rental ID." });
                }

                string hash = await _blockchainService.AppendBlockAsync(
                    req.RentalId,
                    string.IsNullOrWhiteSpace(req.ActionType) ? "CONTRACT_SEALED" : req.ActionType,
                    req.ContractData);

                return Ok(new
                {
                    message = "Contract block successfully committed to immutable cryptographic ledger.",
                    rentalId = req.RentalId,
                    blockHash = hash
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // GET /api/blockchain/contracts/{rentalId}
        [HttpGet("contracts/{rentalId:int}")]
        public async Task<IActionResult> GetContracts(int rentalId)
        {
            try
            {
                var blocks = await _blockchainService.GetRentalBlocksAsync(rentalId);
                if (blocks.Count == 0)
                {
                    return NotFound(new { message = $"No blockchain records found for rental #{rentalId}." });
                }

                return Ok(blocks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // GET /api/blockchain/verify/{rentalId}
        [HttpGet("verify/{rentalId:int}")]
        public async Task<IActionResult> VerifyChain(int rentalId)
        {
            try
            {
                var result = await _blockchainService.VerifyRentalChainAsync(rentalId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }

    public class BlockchainRequest
    {
        public int RentalId { get; set; }
        public string ActionType { get; set; } = "CONTRACT_SEALED";
        public object ContractData { get; set; } = string.Empty;
    }
}
