using Blaze.Helpers;
using Blaze.Search;

namespace Blaze
{
    /// <summary>
    /// Handles communication between the engine and a GUI using
    /// the Universal Chess Interface (UCI) protocol.
    /// </summary>
    public class UCI
    {
        private readonly Engine engine;

        public UCI()
        {
            engine = new Engine();
            engine.OnMoveChosen += OnMoveChosen;
        }

        /// <summary>
        /// Processes a UCI command received from the GUI.
        /// </summary>
        public void ProcessCommand(string message)
        {
            string[] tokens = message.Split(' ');

            switch (tokens[0])
            {
                case "uci":
                    Respond("id name Blaze");
                    Respond("id author ItsExtra");
                    Respond("uciok");
                    break;

                case "isready":
                    Respond("readyok");
                    break;

                case "ucinewgame":
                    break;

                case "position":
                    MatchManager.LoadPositionCommand(tokens);
                    break;

                case "go":
                    if (tokens.Length == 3 &&
                        tokens[1] == "perft" &&
                        int.TryParse(tokens[2], out int depth))
                    {
                        Notation.PrintPerftTest(MatchManager.board, depth);
                        return;
                    }

                    ProcessGoCommand(tokens);
                    break;

                case "stop":
                    engine.EndSearch();
                    break;

                case "quit":
                    Environment.Exit(0);
                    break;

                case "d":
                    Board.PrintBoard(MatchManager.board);
                    break;

                default:
                    Respond($"Unknown command: {message}");
                    break;
            }
        }

        /// <summary>
        /// Sends a response to the GUI.
        /// </summary>
        private static void Respond(string message)
        {
            Console.WriteLine(message);
        }

        /// <summary>
        /// Processes the UCI "go" command and starts a search.
        /// </summary>
        private void ProcessGoCommand(string[] tokens)
        {
            engine.EndSearch();

            if (tokens.Length > 1)
            {
                switch (tokens[1])
                {
                    case "infinite":
                        engine.StartThinkingInfinite();
                        return;
                    case "movetime":
                        if (tokens.Length > 2 && int.TryParse(tokens[2], out int moveTime))
                        {
                            engine.StartThinkingTimed(moveTime);
                        }
                        return;

                    default:
                        break;
                }
            }

            if (engine.UseMaxTimePerMove)
            {
                engine.StartThinkingTimed(engine.MaxTimePerMoveInMs);
                return;
            }

            int whiteTime = 0;
            int blackTime = 0;
            int whiteIncrement = 0;
            int blackIncrement = 0;

            for (int i = 0; i < tokens.Length; i++)
            {
                switch (tokens[i])
                {
                    case "wtime":
                        whiteTime = int.Parse(tokens[i + 1]);
                        break;

                    case "btime":
                        blackTime = int.Parse(tokens[i + 1]);
                        break;

                    case "winc":
                        whiteIncrement = int.Parse(tokens[i + 1]);
                        break;

                    case "binc":
                        blackIncrement = int.Parse(tokens[i + 1]);
                        break;
                }
            }

            int thinkTime = engine.ChooseThinkTime(
                whiteTime,
                blackTime,
                whiteIncrement,
                blackIncrement);

            engine.StartThinkingTimed(thinkTime);
        }

        /// <summary>
        /// Sends the selected move to the GUI.
        /// </summary>
        private void OnMoveChosen(string move)
        {
            Respond($"bestmove {move}");
        }
    }
}