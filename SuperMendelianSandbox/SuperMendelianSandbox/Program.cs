using System;
using System.IO;

namespace SMS
{
    class Program
    {
        static void Main(string[] args)
        {
            Simulation Sim1 = new Simulation();

            if (args.Length > 0 && File.Exists(args[0]))
            {
                Console.WriteLine("Loading config from: " + args[0]);
                string json = File.ReadAllText(args[0]);
                Sim1.ApplyConfig(json);
            }

            Console.WriteLine("Initializing...");
            Sim1.Simulate();
            Console.WriteLine("Simulation Ends.");
        }
    }
}
