using System;

namespace SMS
{
    /// <summary>
    /// Entry point for the Super Mendelian Sandbox (SMS) gene drive simulation.
    /// Creates a Simulation instance with default parameters and runs the main
    /// simulation loop. Alternative entry points for parameter sweeps
    /// (SimulateSweep) and time-to-extinction analysis (SimulateTimeSweep) are
    /// available but currently commented out.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Simulation Sim1 = new Simulation();

            Console.WriteLine("Initializing...");

            // Run the primary multi-generation, multi-iteration simulation.
            Sim1.Simulate();
            //Sim1.SimulateSweep();       // Parameter sweep across HDR, Cas9, and conservation values
            //Sim1.SimulateTimeSweep();    // Time-to-extinction analysis across parameter space

            Console.WriteLine("Simulation Ends.");

        }
    }
}
