using System;

namespace DriveAndGo_API.Tests.Fixtures
{
    public static class EnvironmentGuard
    {
        public static void AssertSafeEnvironment(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("❌ [GUARD] Connection string is empty. Execution aborted.");
            }

            // Ensure connection string only points to localhost (Docker PG)
            if (!connectionString.Contains("localhost") && !connectionString.Contains("127.0.0.1") && !connectionString.Contains("postgres_db"))
            {
                throw new InvalidOperationException(
                    $"❌ [GUARD] PRODUCTION POLLUTION PREVENTED. Test connection string does not target localhost or postgres_db. Connection string: {connectionString}");
            }

            // Explicitly block Supabase keywords
            var forbiddenKeywords = new[] { "supabase", "aws", "neon", ".com", ".io" };
            foreach (var keyword in forbiddenKeywords)
            {
                if (connectionString.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"❌ [GUARD] PRODUCTION POLLUTION PREVENTED. Test connection string contains remote keyword '{keyword}'. Connection string: {connectionString}");
                }
            }

            Console.WriteLine("✅ [GUARD] Environment verified as safe. Proceeding with tests against Docker PostgreSQL.");
        }
    }
}
