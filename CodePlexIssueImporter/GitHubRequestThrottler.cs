using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Octokit;

namespace CodePlexIssueImporter
{
    public class GitHubRequestThrottler
    {
        private static readonly TimeSpan _minTimeSpan = TimeSpan.FromSeconds(1);
        private readonly GitHubClient _client;
        private MiscellaneousRateLimit _rateLimit;
        private DateTimeOffset _lastRateLimit = DateTimeOffset.Now;
        private DateTimeOffset _lastRequest = DateTimeOffset.Now - TimeSpan.FromMinutes(1);
        private TimeSpan _currentDelay = TimeSpan.FromSeconds(1);
        private long _apiRemainingDifference = 0;
        private long _lastApiRemaining = int.MaxValue;

        public GitHubRequestThrottler(GitHubClient client)
        {
            _client = client;
        }

        public async Task Throttle()
        {
            await UpdateRateLimit();
            var now = DateTimeOffset.Now;
            var elapsed = now - _lastRequest;
            var remaining = _currentDelay - elapsed;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining);
            _lastRequest = DateTimeOffset.Now;
        }

        public void UpdateLastRequest()
        {
            _lastRequest = DateTimeOffset.Now;
        }

        private async Task UpdateRateLimit()
        {
            var now = DateTimeOffset.Now;
            var isInitialCall = _rateLimit == null;
            if (!isInitialCall)
            {
                var difference = now - _lastRateLimit;
                if (difference < TimeSpan.FromSeconds(30))
                    return;
            }

            _rateLimit = await _client.Miscellaneous.GetRateLimits();
            _lastRateLimit = now;

            if (isInitialCall)
            {
                _lastApiRemaining = _rateLimit.Resources.Core.Remaining;
            }

            var apiRemainingDifference = _rateLimit.Resources.Core.Remaining - _lastApiRemaining;
            _apiRemainingDifference += apiRemainingDifference;
            _lastApiRemaining = _rateLimit.Resources.Core.Remaining;
            if (_apiRemainingDifference < 0)
            {
                _currentDelay += TimeSpan.FromSeconds(0.5);
            }
            else if (_apiRemainingDifference > 0)
            {
                _currentDelay -= TimeSpan.FromSeconds(0.5);
                if (_currentDelay < _minTimeSpan)
                    _currentDelay = _minTimeSpan;
            }
        }
    }
}
