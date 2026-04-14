using DriveAndGo_API.Models;
using DriveAndGo_API.Services;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace DriveAndGo_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class IssuesController : ControllerBase
{
    private readonly string _connectionString;
    private readonly NotificationWriter _notificationWriter;

    public IssuesController(IConfiguration configuration, NotificationWriter notificationWriter)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _notificationWriter = notificationWriter;
    }

    [HttpPost("report")]
    public IActionResult ReportIssue([FromBody] Issue issue)
    {
        if (issue.RentalId <= 0 || issue.ReporterId <= 0 || string.IsNullOrWhiteSpace(issue.Description))
        {
            return BadRequest(new { Message = "RentalId, reporterId, and description are required." });
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var insertCommand = new MySqlCommand(
                @"INSERT INTO issues
                    (rental_id, reporter_id, issue_type, description, image_url, status, reported_at)
                  VALUES
                    (@rental_id, @reporter_id, @issue_type, @description, @image_url, 'Pending', NOW())",
                connection);

            insertCommand.Parameters.AddWithValue("@rental_id", issue.RentalId);
            insertCommand.Parameters.AddWithValue("@reporter_id", issue.ReporterId);
            insertCommand.Parameters.AddWithValue("@issue_type", string.IsNullOrWhiteSpace(issue.IssueType) ? "General" : issue.IssueType.Trim());
            insertCommand.Parameters.AddWithValue("@description", issue.Description.Trim());
            insertCommand.Parameters.AddWithValue("@image_url", string.IsNullOrWhiteSpace(issue.ImageUrl) ? DBNull.Value : issue.ImageUrl.Trim());
            insertCommand.ExecuteNonQuery();

            var issueId = Convert.ToInt32(new MySqlCommand("SELECT LAST_INSERT_ID()", connection).ExecuteScalar());

            _notificationWriter.Create(
                connection,
                issue.ReporterId,
                "Issue reported",
                "Your issue report was submitted and forwarded to the admin team.",
                "issue");

            return Ok(new { Message = "Issue reported successfully.", IssueId = issueId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Database Error: " + ex.Message });
        }
    }

    [HttpGet]
    public IActionResult GetAllIssues()
    {
        try
        {
            var issues = new List<Issue>();

            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
                @"SELECT
                    i.issue_id,
                    i.rental_id,
                    i.reporter_id,
                    i.issue_type,
                    i.description,
                    i.image_url,
                    i.status,
                    i.reported_at,
                    u.full_name AS reporter_name,
                    CONCAT(v.brand, ' ', v.model, ' (', v.plate_no, ')') AS vehicle_name
                  FROM issues i
                  JOIN users u ON i.reporter_id = u.user_id
                  JOIN rentals r ON i.rental_id = r.rental_id
                  JOIN vehicles v ON r.vehicle_id = v.vehicle_id
                  ORDER BY i.reported_at DESC",
                connection);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                issues.Add(new Issue
                {
                    IssueId = Convert.ToInt32(reader["issue_id"]),
                    RentalId = Convert.ToInt32(reader["rental_id"]),
                    ReporterId = Convert.ToInt32(reader["reporter_id"]),
                    IssueType = reader["issue_type"]?.ToString() ?? string.Empty,
                    Description = reader["description"]?.ToString() ?? string.Empty,
                    ImageUrl = reader["image_url"] == DBNull.Value ? null : reader["image_url"].ToString(),
                    Status = reader["status"]?.ToString() ?? "Pending",
                    ReportedAt = reader["reported_at"] == DBNull.Value ? DateTime.UtcNow : Convert.ToDateTime(reader["reported_at"]),
                    ReporterName = reader["reporter_name"]?.ToString(),
                    VehicleName = reader["vehicle_name"]?.ToString()
                });
            }

            return Ok(issues);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Database Error: " + ex.Message });
        }
    }

    [HttpPatch("{id:int}/status")]
    public IActionResult UpdateIssueStatus(int id, [FromBody] string newStatus)
    {
        var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Pending",
            "In Progress",
            "Resolved"
        };

        if (!validStatuses.Contains(newStatus))
        {
            return BadRequest(new { Message = "Valid statuses: Pending, In Progress, Resolved." });
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = new MySqlCommand(
                "UPDATE issues SET status = @status WHERE issue_id = @id",
                connection);
            command.Parameters.AddWithValue("@status", newStatus);
            command.Parameters.AddWithValue("@id", id);

            if (command.ExecuteNonQuery() == 0)
            {
                return NotFound(new { Message = "Issue not found." });
            }

            return Ok(new { Message = "Issue status updated successfully.", IssueId = id, Status = newStatus });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Database Error: " + ex.Message });
        }
    }
}
