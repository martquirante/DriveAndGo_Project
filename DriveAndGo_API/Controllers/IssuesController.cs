using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using DriveAndGo_API.Models;

namespace DriveAndGo_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IssuesController : ControllerBase
    {
        private readonly string _connectionString;

        public IssuesController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        // ══ POST — Mobile app (Driver/Customer) nag-submit ng report ══
        [HttpPost("report")]
        public IActionResult ReportIssue([FromBody] Issue issue)
        {
            if (issue.RentalId == 0 || issue.ReporterId == 0 || string.IsNullOrWhiteSpace(issue.Description))
                return BadRequest(new { message = "RentalId, ReporterId, and Description are required." });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // I-save ang incident report sa database
                var insertCmd = new MySqlCommand(@"
                    INSERT INTO issues 
                        (rental_id, reporter_id, issue_type, description, image_url, status, reported_at) 
                    VALUES 
                        (@rental_id, @reporter_id, @type, @desc, @img, 'Pending', NOW())", conn);

                insertCmd.Parameters.AddWithValue("@rental_id", issue.RentalId);
                insertCmd.Parameters.AddWithValue("@reporter_id", issue.ReporterId);
                insertCmd.Parameters.AddWithValue("@type", issue.IssueType ?? "General");
                insertCmd.Parameters.AddWithValue("@desc", issue.Description);
                insertCmd.Parameters.AddWithValue("@img", issue.ImageUrl ?? (object)DBNull.Value);

                insertCmd.ExecuteNonQuery();

                return Ok(new { message = "Issue reported successfully. The admin has been notified." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error: " + ex.Message });
            }
        }

        // ══ GET ALL — Para sa Admin Windows App para makita ang mga reklamo ══
        [HttpGet]
        public IActionResult GetAllIssues()
        {
            try
            {
                List<Issue> issues = new List<Issue>();
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // Naka-JOIN para makuha ang pangalan ng nag-report at anong sasakyan
                var cmd = new MySqlCommand(@"
                    SELECT i.*, u.full_name AS reporter_name, 
                           CONCAT(v.brand, ' ', v.model, ' (', v.plate_number, ')') AS vehicle_name
                    FROM issues i
                    JOIN users u ON i.reporter_id = u.user_id
                    JOIN rentals r ON i.rental_id = r.rental_id
                    JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                    ORDER BY i.reported_at DESC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    issues.Add(new Issue
                    {
                        IssueId = Convert.ToInt32(reader["issue_id"]),
                        RentalId = Convert.ToInt32(reader["rental_id"]),
                        ReporterId = Convert.ToInt32(reader["reporter_id"]),
                        IssueType = reader["issue_type"].ToString(),
                        Description = reader["description"].ToString(),
                        ImageUrl = reader["image_url"].ToString(),
                        Status = reader["status"].ToString(),
                        ReportedAt = Convert.ToDateTime(reader["reported_at"]),
                        ReporterName = reader["reporter_name"].ToString(),
                        VehicleName = reader["vehicle_name"].ToString()
                    });
                }

                return Ok(issues);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error: " + ex.Message });
            }
        }

        // ══ PATCH — Update ni Admin kapag na-resolve na ang issue ══
        [HttpPatch("{id}/status")]
        public IActionResult UpdateIssueStatus(int id, [FromBody] string newStatus)
        {
            var validStatuses = new[] { "Pending", "In Progress", "Resolved" };
            if (!validStatuses.Contains(newStatus))
                return BadRequest(new { message = "Invalid status. Use: Pending, In Progress, or Resolved." });

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                var updateCmd = new MySqlCommand(
                    "UPDATE issues SET status = @status WHERE issue_id = @id", conn);
                updateCmd.Parameters.AddWithValue("@status", newStatus);
                updateCmd.Parameters.AddWithValue("@id", id);

                int affected = updateCmd.ExecuteNonQuery();

                if (affected == 0)
                    return NotFound(new { message = "Issue report not found." });

                return Ok(new { message = "Issue status updated to " + newStatus });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error: " + ex.Message });
            }
        }
    }
}