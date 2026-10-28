#nullable enable
using System;
using System.IO;
using NUnit.Framework;

namespace DesktopDrift.Core.Tests
{
    internal static class TestPaths
    {
        private static string? Walk(string start, string relative)
        {
            string? dir = start;
            while (!string.IsNullOrEmpty(dir))
            {
                string candidate = Path.Combine(dir, relative);
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        // Walks up from TestDirectory (then cwd) until <dir>/<relative> exists.
        public static string Locate(string relative)
        {
            string a = TestContext.CurrentContext.TestDirectory;
            string b = Directory.GetCurrentDirectory();
            string? found = Walk(a, relative) ?? Walk(b, relative);
            if (found == null)
            {
                Assert.Fail(relative + " not found walking up from TestDirectory='" + a +
                            "' nor from cwd='" + b + "'");
                throw new InvalidOperationException();
            }
            return found;
        }
    }
}
