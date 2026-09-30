using Blaze.Helpers;
using Blaze.MoveGen;
using Blaze.Search;

namespace Blaze
{
    /// <summary>
    /// Manages search execution, time management, and communication
    /// between the searcher and the UCI layer.
    /// </summary>
    public class Engine
    {
        public bool UseMaxTimePerMove { get; set; } = true;
        public int MaxTimePerMoveInMs { get; set; } = 100;

        public bool IsThinking { get; private set; }

        /// <summary>
        /// Raised when a search completes and a move has been selected.
        /// </summary>
        public event Action<string>? OnMoveChosen; 

        private CancellationTokenSource? searchCancellation;

        public Engine()
        {
            Searcher.OnSearchComplete += OnSearchComplete;
        }

        /// <summary>
        /// Calculates how much time to spend on the current move.
        /// </summary>
        public int ChooseThinkTime(int whiteTimeRemainingMs, int blackTimeRemainingMs, int whiteIncrementMs, int blackIncrementMs)
        {
            Board board = MatchManager.board;

            int remainingTimeMs = board.IsWhiteToMove ? whiteTimeRemainingMs : blackTimeRemainingMs;

            int incrementMs = board.IsWhiteToMove ? whiteIncrementMs : blackIncrementMs;

            double thinkTimeMs = remainingTimeMs / 40.0;

            if (UseMaxTimePerMove)
                thinkTimeMs = Math.Min(MaxTimePerMoveInMs, thinkTimeMs);

            if (remainingTimeMs > incrementMs * 2)
                thinkTimeMs += incrementMs * 0.8;

            double minimumThinkTimeMs =
                Math.Min(50, remainingTimeMs * 0.25);

            return (int)Math.Ceiling(
                Math.Max(minimumThinkTimeMs, thinkTimeMs));
        }

        /// <summary>
        /// Starts a search with a fixed time limit.
        /// </summary>
        public void StartThinkingTimed(int thinkTimeMs)
        {
            IsThinking = true;

            searchCancellation?.Cancel();

            StartTimedSearch(thinkTimeMs);
        }

        /// <summary>
        /// Starts an infinite search that continues until stopped.
        /// </summary>
        public void StartThinkingInfinite()
        {
            IsThinking = true;

            searchCancellation?.Cancel();

            Task.Run(Searcher.StartNewSearch);
        }

        private void StartTimedSearch(int thinkTimeMs)
        {
            searchCancellation = new CancellationTokenSource();

            CancellationTokenSource cancellation = searchCancellation;

            Task.Delay(thinkTimeMs, cancellation.Token).ContinueWith(task =>
                {
                    if (task.IsCanceled || cancellation != searchCancellation || !IsThinking)
                    {
                        return;
                    }

                    EndSearch();
                });

            Searcher.StartNewSearch();
        }

        /// <summary>
        /// Handles completion of the current search.
        /// </summary>
        private void OnSearchComplete(Move move)
        {
            IsThinking = false;

            OnMoveChosen?.Invoke(Notation.MoveToNotation(move));
        }

        /// <summary>
        /// Stops the current search.
        /// </summary>
        public void EndSearch()
        {
            searchCancellation?.Cancel();

            if (IsThinking)
                Searcher.EndSearch();

            IsThinking = false;
        }
    }
}