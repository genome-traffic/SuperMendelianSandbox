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
            //Sim1.SimulateSweep();
            Sim1.SimulateTimeSweep();

            Console.WriteLine("Simulation ends.");




        }
    }
}
