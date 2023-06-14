using System;

namespace SMS
{
    class Program
    {
        static void Main(string[] args)
        {
            Simulation Sim1 = new Simulation();

            Console.WriteLine("Initializing simulation...");

            //Sim1.Simulate();

            Console.WriteLine("Simulation ends.");

            Console.WriteLine("Initializing sweep...");

            Sim1.SimulateSweep();

            Console.WriteLine("Sweep ends.");

        }
    }
}
