using System;
using SenseNet.DirectoryServices;
using SenseNet.ContentRepository;

namespace ADSync
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var startSettings = new RepositoryStartSettings
            {
                Console = Console.Out,
                StartLuceneManager = true
            };
            using (Repository.Start(startSettings))
            {
                ADProvider.RetryAllFailedActions();
            }
        }
    }
}
