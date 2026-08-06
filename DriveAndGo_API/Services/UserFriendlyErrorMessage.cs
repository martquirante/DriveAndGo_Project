namespace DriveAndGo_API.Services
{
    public static class UserFriendlyErrorMessage
    {
        public static string Clean(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "We encountered a temporary issue. Please try again in a moment.";

            string lower = message.ToLowerInvariant();

            // 1. Database / SQL Constraint errors
            if (lower.Contains("23505") || lower.Contains("duplicate key") || lower.Contains("already exists") || lower.Contains("unique constraint"))
                return "A record with the same details (such as plate number, email, or booking) already exists. Please review the information and try again.";

            if (lower.Contains("23503") || lower.Contains("foreign key") || lower.Contains("violates foreign key constraint"))
                return "This action cannot be completed because this record is linked to other active items in the system.";

            if (lower.Contains("db error") || lower.Contains("postgres") || lower.Contains("npgsql") || lower.Contains("connection string") || lower.Contains("database"))
                return "Our database service is temporarily unreachable. Please check your connection and try again in a moment.";

            // 2. AI & Cloud API quota/key errors
            if (lower.Contains(".env") || lower.Contains("api key") || lower.Contains("groq") || lower.Contains("gemini") || lower.Contains("quota") || lower.Contains("rate limit"))
                return "Drive&Go AI is currently processing a high volume of requests. Please try asking your question again in a moment.";

            // 3. Null reference / Index / Code exception artifacts
            if (lower.Contains("nullreference") || lower.Contains("object reference") || lower.Contains("indexoutofrange") || lower.Contains("argumentoutofrange") || lower.Contains("system.exception"))
                return "Some required information is missing or incomplete. Please check your input and try again.";

            // 4. Auth & Permission errors
            if (lower.Contains("unauthorized") || lower.Contains("invalid credentials") || lower.Contains("maling password"))
                return "Incorrect email or password. Please verify your login credentials.";

            if (lower.Contains("expired") || lower.Contains("token"))
                return "Your session has expired. Please log in again to continue.";

            // 5. Generic technical error fallback
            if (lower.Contains("exception") || lower.Contains("error:") || lower.Contains("failed to") || lower.Contains("stacktrace") || lower.Contains("line "))
                return "We encountered a temporary issue processing your request. Please try again in a moment.";

            return message;
        }
    }
}
