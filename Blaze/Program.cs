using Blaze.MoveGen;

namespace Blaze
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Initialize all precomputed lookup tables.
            PrecomputeMoveData.Init();
            Magic.Init();

            UCI uci = new();

            // Continuously process commands received from the GUI.
            while (true)
            {
                string command = Console.ReadLine();

                if (command is null)
                    break;

                uci.ProcessCommand(command);
            }
        }
    }
}
