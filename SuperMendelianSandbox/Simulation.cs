using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Xml.Linq;
using System.Threading.Tasks;

namespace SMS
{
    class Simulation
    {

        /*-------------------- Simulation Parameters ---------------------------------*/

        public int Generations = 20;
        public int Iterations = 10;

        public int PopulationCap = 1000;
        public float Mortality = 0.1f;
        public float ZygoticHDRReduction = 0.99F;
        public int GlobalEggsPerFemale = 50;
        public int Sample = 48;

        public bool ApplyIntervention = false;
        public int StartIntervention = 2;
        public int EndIntervention = 2;
        public int InterventionReleaseNumber = 100;


        // Sweep param1 for HDR
        public static float Param0;
        List<float> P0list = new List<float>() { 0.75F, 0.80F, 0.85F, 0.9F, 0.95F, 1F };

        // Sweep param1 for Cas9 activity
        public static float Param1;
        List<float> P1list = new List<float>() { 0.75F, 0.8F, 0.85F, 0.9F, 0.95F, 1F };

        // Sweep param1 for r1
        public static float Param2;
        //List<float> P2list = new List<float>() { 0.9F, 0.92F, 0.94F, 0.96F, 0.98F, 1F };
        List<float> P2list = new List<float>() { 0.99F };


        string[] Track = { "TRA","FFER"};

        public static string[,] Target_cognate_gRNA = { { "FFER", "gRNA_FFER" }, { "TRA", "gRNA_TRA" } };

        /*------------------------------- The Simulation ---------------------------------------------*/

        public void Simulate()
        { 
            string pathdesktop = (string)Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            pathdesktop = pathdesktop + "/model";
            string pathString = System.IO.Path.Combine(pathdesktop, "modeloutput.csv");
            Console.WriteLine("Writing output to: " + pathString);
            File.Create(pathString).Dispose();

            Console.WriteLine("Simulation Starts.");

            using (var stream = File.OpenWrite(pathString))
            using (var Fwriter = new StreamWriter(stream))
            {
                // THE ACTUAL SIMULATION
                for (int cIterations = 1; cIterations <= Iterations; cIterations++)
                {
                    Console.WriteLine("Iteration " + cIterations + " out of " + Iterations);


                    //Population Pop = new Population(150);
                    Population Pop = new Population("cage setup");

                    for (int cGenerations = 1; cGenerations <= Generations; cGenerations++)
                    {
                        //if (ApplyIntervention)
                        //{
                        //    if ((cGenerations >= StartIntervention) && (cGenerations <= EndIntervention))
                        //    {
                        //        Pop = new Population(Pop, new Population("standard release", InterventionReleaseNumber));
                        //    }
                        //
                        //}

                        #region output adult data to file

                        //------------------------ Genotypes -------

                        List<string> Genotypes = new List<string>();

                         foreach (Organism O in Pop.Adults)
                         {
                             foreach (string s in Track)
                             {
                                Genotypes.Add(s + "," + O.GetGenotype(s));
                             }
                         }

                        var queryG = Genotypes.GroupBy(s => s)
                           .Select(g => new { Name = g.Key, Count = g.Count() });

                        foreach (var result in queryG)
                        {
                          Fwriter.WriteLine("{0},{1},{2},{3},all", cIterations, cGenerations, result.Name, result.Count);
                        }

                        Genotypes.Clear();

                        int cSample = Sample;
                        foreach (Organism O in Pop.Adults)
                        {
                            if (cSample > 0)
                            {
                                foreach (string s in Track)
                                {
                                    Genotypes.Add(s + "," + O.GetGenotype(s));
                                }
                                cSample--;
                            }
                        }

                        var queryGs = Genotypes.GroupBy(s => s)
                           .Select(g => new { Name = g.Key, Count = g.Count() });

                        foreach (var result in queryGs)
                        {
                            Fwriter.WriteLine("{0},{1},{2},{3},sample", cIterations, cGenerations, result.Name, result.Count);
                        }

                        //------------------------- Sex -----------
                        int numberofallmales = 0;
                        int numberofallfemales = 0;
                        foreach (Organism O in Pop.Adults)
                        {
                            if (O.GetSex() == "female")
                                numberofallfemales++;
                            else
                                numberofallmales++;
                        }
                        Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Sex", "Males", "NA", numberofallmales, "all");
                        Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Sex", "Females", "NA", numberofallfemales, "all");

                        //------------------------- Sex Karyotype -----------
                        int numberofXX = 0;
                        int numberofXY = 0;
                        foreach (Organism O in Pop.Adults)
                        {

                            switch (O.GetSexChromKaryo())
                            {
                                case "XX":
                                    {
                                        numberofXX++;
                                        break;
                                    }
                                case "XY":
                                    {
                                        numberofXY++;
                                        break;
                                    }
                                case "YX":
                                    {
                                        numberofXY++;
                                        break;
                                    }
                                default:
                                    {
                                        Console.WriteLine(O.GetSexChromKaryo() + " should not exist!");
                                        break;
                                    }
                            }

                        }
                        Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Sex_Karyotype", "XX", "NA", numberofXX, "all");
                        Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Sex_Karyotype", "XY", "NA", numberofXY, "all");


                        #endregion

                        #region Cross all adults and return eggs for next generation

                        Pop.ReproduceToEggs(Mortality,PopulationCap, GlobalEggsPerFemale);

                        Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Eggs", "NA", "NA", Pop.Eggs.Count.ToString(), "all");

                        int EggsToBeReturned = 0;

                        if (Pop.Eggs.Count <= PopulationCap)
                            EggsToBeReturned = Pop.Eggs.Count;
                        else
                            EggsToBeReturned = PopulationCap;

                        for (int na = 0; na < EggsToBeReturned; na++)
                        {
                            Pop.Adults.Add(new Organism(Pop.Eggs[na]));
                        }

                        Pop.Eggs.Clear();

                        Pop.ParentalEffect(ZygoticHDRReduction);

                        #endregion

                    }
                }
                // END OF SIMULATION

                Fwriter.Flush();
            }
        }

        public void SimulateSweep()
        {
            string pathdesktop = (string)Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            pathdesktop = pathdesktop + "/model";
            string pathString = System.IO.Path.Combine(pathdesktop, "modelsweepoutput.csv");
            Console.WriteLine("Writing output to: " + pathString);
            File.Create(pathString).Dispose();
            Console.WriteLine("Simulation Starts.");

            using (var stream = File.OpenWrite(pathString))
            using (var Fwriter = new StreamWriter(stream))
            {
                // THE ACTUAL SIMULATION

                

                foreach (float p0 in P0list)
                {
                    Param0 = p0;

                    foreach (float p1 in P1list)
                    {
                        Param1 = p1;

                        foreach (float p2 in P2list)
                        {
                            Param2 = p2;

                            Parallel.For(0, Iterations, i =>
                            {
                                Console.WriteLine("Iteration " + i.ToString() + " out of " + Iterations);
                                Console.WriteLine("Param0 = " + Param0.ToString() + " , Param1 = " + Param1.ToString() + " and Param2 = " + Param2.ToString());

                                Population Pop = new Population("cage setup");

                                for (int cGenerations = 1; cGenerations <= Generations; cGenerations++)
                                {
                                    //if (ApplyIntervention)
                                    //{
                                    //    if ((cGenerations >= StartIntervention) && (cGenerations <= EndIntervention))
                                    //    {
                                    //        Pop = new Population(Pop, new Population("standard release", InterventionReleaseNumber));
                                    //    }
                                    //}

                                    if (cGenerations == Generations)
                                        Fwriter.WriteLine("{0},{1},{2},{3},{4}", i, Param0.ToString(), Param1.ToString(), Param2.ToString(), Pop.Adults.Count().ToString());

                                    Pop.ReproduceToEggs(Mortality, PopulationCap, GlobalEggsPerFemale);

                                    //Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Eggs", "NA", "NA", Pop.Eggs.Count.ToString(), "all");

                                    int EggsToBeReturned = 0;

                                    if (Pop.Eggs.Count <= PopulationCap)
                                        EggsToBeReturned = Pop.Eggs.Count;
                                    else
                                        EggsToBeReturned = PopulationCap;

                                    for (int na = 0; na < EggsToBeReturned; na++)
                                    {
                                        Pop.Adults.Add(new Organism(Pop.Eggs[na]));
                                    }

                                    Pop.Eggs.Clear();

                                    Pop.ParentalEffect(ZygoticHDRReduction);

                                }
                            });
                        }
                       
                    }
                 }

                // END OF SIMULATION

                Fwriter.Flush();
            }
        }


        public void SimulateTimeSweep()
        {
            this.Generations = 100;
            string pathdesktop = (string)Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            pathdesktop = pathdesktop + "/model";
            string pathString = System.IO.Path.Combine(pathdesktop, "modeltimesweepoutput.csv");
            Console.WriteLine("Writing output to: " + pathString);
            File.Create(pathString).Dispose();
            Console.WriteLine("Simulation Starts.");

            using (var stream = File.OpenWrite(pathString))
            using (var Fwriter = new StreamWriter(stream))
            {
                // THE ACTUAL SIMULATION



                foreach (float p0 in P0list)
                {
                    Param0 = p0;

                    foreach (float p1 in P1list)
                    {
                        Param1 = p1;

                        foreach (float p2 in P2list)
                        {
                            Param2 = p2;

                            Parallel.For(0, Iterations, i =>
                            {
                                Console.WriteLine("Iteration " + i.ToString() + " out of " + Iterations);
                                Console.WriteLine("Param0 = " + Param0.ToString() + " , Param1 = " + Param1.ToString() + " and Param2 = " + Param2.ToString());
                                Population Pop = new Population("cage setup");
                                for (int cGenerations = 1; cGenerations <= Generations; cGenerations++)
                                {
                                    //if (ApplyIntervention)
                                    //{
                                    //    if ((cGenerations >= StartIntervention) && (cGenerations <= EndIntervention))
                                    //    {
                                    //        Pop = new Population(Pop, new Population("standard release", InterventionReleaseNumber));
                                    //    }
                                    //}
                                    //if (cGenerations == Generations)
                                    //    Fwriter.WriteLine("{0},{1},{2},{3},{4}", i, Param0.ToString(), Param1.ToString(), Param2.ToString(), Pop.Adults.Count().ToString());
                                    if (Pop.Adults.Count() == 0)
                                    {
                                        Fwriter.WriteLine("{0},{1},{2},{3},{4}", i, Param0.ToString(), Param1.ToString(), Param2.ToString(), cGenerations.ToString());
                                        break;
                                    }
                                    if (cGenerations == 100)
                                    {
                                        string na = "NA";
                                        Fwriter.WriteLine("{0},{1},{2},{3},{4}", i, Param0.ToString(), Param1.ToString(), Param2.ToString(), na.ToString());
                                        break;
                                    }
                                    Pop.ReproduceToEggs(Mortality, PopulationCap, GlobalEggsPerFemale);
                                    //Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Eggs", "NA", "NA", Pop.Eggs.Count.ToString(), "all");
                                    int EggsToBeReturned = 0;
                                    if (Pop.Eggs.Count <= PopulationCap)
                                        EggsToBeReturned = Pop.Eggs.Count;
                                    else
                                        EggsToBeReturned = PopulationCap;
                                    for (int na = 0; na < EggsToBeReturned; na++)
                                    {
                                        Pop.Adults.Add(new Organism(Pop.Eggs[na]));
                                    }
                                    Pop.Eggs.Clear();
                                    Pop.ParentalEffect(ZygoticHDRReduction);
                                }
                            });
                        }

                    }
                }


                // END OF SIMULATION

                Fwriter.Flush();
            }
        }

    }
}
