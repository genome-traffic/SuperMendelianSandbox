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
    /// <summary>
    /// Master simulation controller for the Super Mendelian Sandbox gene drive model.
    /// Configures all parameters, creates the spatial environment, runs the multi-generation
    /// simulation loop, applies gene drive interventions, and writes output to CSV.
    ///
    /// The simulation models a CRISPR-based sex-distortion gene drive targeting the
    /// TRA (Transformer) gene in a dipteran insect (modeled after Ceratitis capitata /
    /// medfly or similar tephritid). The drive disrupts female sex determination by
    /// converting WT TRA alleles to Transgene copies (via HDR) or resistance alleles
    /// (via NHEJ), progressively masculinizing the population and causing collapse.
    ///
    /// Simulation structure:
    ///   - Multiple independent iterations (replicates) for statistical analysis.
    ///   - Each iteration creates a fresh metapopulation environment.
    ///   - Each generation: record data → apply intervention → reproduce → regulate →
    ///     apply zygotic effects → migrate.
    ///   - Output: genotype frequencies, sex ratios, karyotypes, and egg counts per
    ///     population per generation, written to CSV.
    /// </summary>
    class Simulation
    {

        /*-------------------- Simulation Parameters ---------------------------------*/

        /// <summary>Number of discrete, non-overlapping generations to simulate.</summary>
        public int Generations = 50;

        /// <summary>Number of independent replicate runs. Each iteration starts with a
        /// fresh environment to capture stochastic variation.</summary>
        public int Iterations = 3;

        /// <summary>Per-generation natural mortality rate (0–1). Controls the number of
        /// mate-finding attempts in ReproduceToEggs: EffectivePopulation = (1-Mortality)*cap.
        /// Higher mortality → harder to find mates → lower effective reproduction.</summary>
        public float Mortality = 0.1f;

        /// <summary>Fractional reduction in homology-directed repair (HDR) efficiency for
        /// zygotic (embryonic) gene drive activity. 0.99 = 99% reduction compared to
        /// germline HDR. This makes zygotic cutting overwhelmingly produce resistance
        /// alleles (NHEJ) rather than drive copies (HDR).</summary>
        public float ZygoticHDRReduction = 0.99F;

        /// <summary>Base number of eggs produced per successful female mating event.
        /// Modulated by parental fertility values (currently always 1.0).</summary>
        public int GlobalEggsPerFemale = 50;

        /// <summary>Number of organisms to sample from each population for "sample"
        /// genotype output (simulates field sampling of a subset).</summary>
        public int Sample = 48;

        /// <summary>Whether to release gene drive organisms during the simulation.</summary>
        public bool ApplyIntervention = true;

        /// <summary>First generation in which gene drive males are released (inclusive).</summary>
        public int StartIntervention = 2;

        /// <summary>Last generation in which gene drive males are released (inclusive).
        /// When equal to StartIntervention, release occurs in a single generation.</summary>
        public int EndIntervention = 2;

        /// <summary>Number of gene drive males released per intervention generation
        /// into population 0.</summary>
        public int InterventionReleaseNumber = 100;

        /// <summary>Param0: Homology-directed repair (HDR) efficiency (0–1).
        /// Used as the "HomRepair_male" and "HomRepair_female" trait value for both
        /// WT target loci and the Transgene. Higher values → more successful gene drive
        /// homing. Default 0.95 = 95% HDR success when Cas9 cuts.</summary>
        public static float Param0 = 0.95F;
        //List<float> P0list = new List<float>() { 0.75F, 0.80F, 0.85F, 0.9F, 0.95F, 1F };

        /// <summary>Param1: Cas9 nuclease activity level (0–1).
        /// Used as the "Cas9_male", "Cas9_female", and "Cas9_maternal" trait values
        /// for the Transgene. Higher values → higher probability of cutting WT alleles
        /// during meiosis and in embryos. Default 0.95 = 95% cutting probability.</summary>
        public static float Param1 = 0.95F;
        //List<float> P1list = new List<float>() { 0.75F, 0.8F, 0.85F, 0.9F, 0.95F, 1F };

        /// <summary>Param2: Conservation level (0–1).
        /// Used as the "Conservation" trait at WT loci. Determines the probability that
        /// NHEJ repair at a cut site produces an R2 (loss-of-function, non-functional
        /// resistance) allele vs an R1 (functional resistance) allele. Higher values →
        /// more R2 (good for the drive, since R2 cannot rescue gene function).
        /// Default 0.999 = nearly all NHEJ produces R2.</summary>
        public static float Param2 = 0.999F;
        ////List<float> P2list = new List<float>() { 0.9F, 0.92F, 0.94F, 0.96F, 0.98F, 1F };
        //List<float> P2list = new List<float>() { 0.99F };

        /// <summary>Names of genes whose genotype frequencies are tracked in the output.
        /// TRA is the primary gene drive target; FFER is a secondary target locus.</summary>
        string[] Track = {"TRA","FFER"};

        /// <summary>Defines which gRNA targets which gene. Each row is {target_gene, gRNA_name}.
        /// The gene drive's Cas9 uses each gRNA to cut the corresponding target gene.
        /// Row 0: FFER targeted by gRNA_FFER; Row 1: TRA targeted by gRNA_TRA.
        /// This is a static field accessed throughout the simulation by Chromosome and
        /// Organism classes during gene drive mechanics.</summary>
        public static string[,] Target_cognate_gRNA = { { "FFER", "gRNA_FFER" }, { "TRA", "gRNA_TRA" } };

        /*------------------------------- The Simulation ---------------------------------------------*/

        /// <summary>
        /// Main simulation method. Runs the complete multi-iteration, multi-generation
        /// gene drive simulation and writes all output to a CSV file on the Desktop.
        ///
        /// Output file: ~/Desktop/model/modeloutput.csv
        /// CSV columns (no header row):
        ///   Iteration, Population, Generation, Category, Allele1, Allele2, Count, Type
        ///
        /// Output categories per population per generation:
        ///   - Genotype frequencies for tracked genes (TRA, FFER) — both "all" (full census)
        ///     and "sample" (first N=48 organisms, simulating field sampling).
        ///   - Phenotypic sex counts (Males, Females) — "all" census.
        ///   - Karyotype counts (XX, XY) — "all" census.
        ///   - Total egg count produced — "all".
        ///
        /// Simulation flow per generation per population:
        ///   1. Apply intervention: if enabled and within the intervention window, release
        ///      InterventionReleaseNumber gene drive males into population 0.
        ///   2. Record output data (genotypes, sex ratios, karyotypes).
        ///   3. Reproduce: all females attempt to mate with random males, producing eggs.
        ///      Adults die (non-overlapping generations).
        ///   4. Record egg count.
        ///   5. Density regulation: promote up to PopulationCapacity eggs to adults.
        ///   6. Apply parental effects: zygotic Cas9 activity on new adults.
        ///   7. After all populations are processed: execute inter-population migration.
        ///
        /// Environment setup:
        ///   6 populations of 500 individuals each (cap 500), connected by migration
        ///   with exponentially decreasing rates:
        ///     Pop 0↔1: 10%,  1↔2: 1%,  3↔4: 0.1%,  4↔5: 0.01%, ...
        ///   Note: Migration is defined for population indices beyond the 6 that exist
        ///   (indices 5–9), which are out of range and have no effect.
        /// </summary>
        public void Simulate()
        {
            // Set up output file path on the Desktop
            string pathdesktop = (string)Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            pathdesktop = pathdesktop + "/model";
            string pathString = System.IO.Path.Combine(pathdesktop, "modeloutput.csv");
            Console.WriteLine("Writing output to: " + pathString);
            File.Create(pathString).Dispose();

            Console.WriteLine("Simulation Starts.");

            using (var stream = File.OpenWrite(pathString))
            using (var Fwriter = new StreamWriter(stream))
            {
                // === MAIN SIMULATION LOOP ===
                for (int cIterations = 1; cIterations <= Iterations; cIterations++)
                {
                    Console.WriteLine("Iteration " + cIterations + " out of " + Iterations);

                    // Create a fresh metapopulation environment for each replicate:
                    // 6 populations, each with 500 WT organisms and cap 500.
                    Environ Africa = new Environ(6,500,500);

                    // Define migration connectivity between populations.
                    // Rates decrease exponentially with distance.
                    // Note: Indices 5-9 exceed the 6 populations (0-5) so those
                    // DefineMigration calls target non-existent populations.
                    Africa.DefineMigration(0, 1, 0.1F);
                    Africa.DefineMigration(1, 2, 0.01F);
                    Africa.DefineMigration(3, 4, 0.001F);
                    Africa.DefineMigration(4, 5, 0.0001F);
                    Africa.DefineMigration(5, 6, 0.00001F);      // Pop 6 doesn't exist
                    Africa.DefineMigration(6, 7, 0.000001F);     // Pop 6,7 don't exist
                    Africa.DefineMigration(7, 8, 0.0000001F);    // Pop 7,8 don't exist
                    Africa.DefineMigration(8, 9, 0.00000001F);   // Pop 8,9 don't exist

                    // === GENERATION LOOP ===
                    for (int cGenerations = 1; cGenerations <= Generations; cGenerations++)
                    {
                        // Process each population independently
                        for (var p = 0; p < Africa.Populations.Count; p++)
                        {
                            // --- Step 1: Gene drive intervention (release) ---
                            if (ApplyIntervention)
                            {
                                if ((cGenerations >= StartIntervention) && (cGenerations <= EndIntervention))
                                {
                                    // Release drive males only into population 0 (the target site)
                                    if (p == 0)
                                    {
                                        Population Release = new Population(InterventionReleaseNumber);
                                        Africa.Populations[p].AddToPopulation(Release);
                                    }
                                }
                            }

                            #region Output adult data to file

                            // --- Step 2a: Record full-census genotype frequencies ---
                            // For each tracked gene, collect the genotype of every adult
                            // and group-count them.
                            List<string> Genotypes = new List<string>();

                            foreach (Organism O in Africa.Populations[p].Adults)
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
                                Fwriter.WriteLine("{0},{1},{2},{3},{4},all", cIterations, p, cGenerations, result.Name, result.Count);
                            }

                            // --- Step 2b: Record sampled genotype frequencies ---
                            // Take the first `Sample` organisms as a simulated field sample
                            Genotypes.Clear();

                            int cSample = Sample;
                            foreach (Organism O in Africa.Populations[p].Adults)
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
                                Fwriter.WriteLine("{0},{1},{2},{3},{4},sample", cIterations, p, cGenerations, result.Name, result.Count);
                            }

                            // --- Step 2c: Record phenotypic sex ratio ---
                            int numberofallmales = 0;
                            int numberofallfemales = 0;
                            foreach (Organism O in Africa.Populations[p].Adults)
                            {
                                if (O.GetSex() == "female")
                                    numberofallfemales++;
                                else
                                    numberofallmales++;
                            }
                            Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7}", cIterations, p, cGenerations, "Sex", "Males", "NA", numberofallmales, "all");
                            Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7}", cIterations, p, cGenerations, "Sex", "Females", "NA", numberofallfemales, "all");

                            // --- Step 2d: Record sex chromosome karyotype counts ---
                            // Distinguishes genetic sex (XX/XY) from phenotypic sex
                            // (which can differ when TRA is disrupted)
                            int numberofXX = 0;
                            int numberofXY = 0;
                            foreach (Organism O in Africa.Populations[p].Adults)
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
                                            numberofXY++;   // YX is same as XY, just different list ordering
                                            break;
                                        }
                                    default:
                                        {
                                            Console.WriteLine(O.GetSexChromKaryo() + " should not exist!");
                                            break;
                                        }
                                }

                            }
                            Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7}", cIterations, p, cGenerations, "Sex_Karyotype", "XX", "NA", numberofXX, "all");
                            Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7}", cIterations, p, cGenerations, "Sex_Karyotype", "XY", "NA", numberofXY, "all");


                            #endregion

                            #region Cross all adults and return eggs for next generation

                            // --- Step 3: Reproduction ---
                            // All females attempt to mate. Adults die. Eggs are produced.
                            Africa.Populations[p].ReproduceToEggs(Mortality, Africa.Populations[p].PopulationCapacity, GlobalEggsPerFemale);

                            // Record total egg count
                            Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7}", cIterations, p, cGenerations, "Eggs", "NA", "NA", Africa.Populations[p].Eggs.Count.ToString(), "all");

                            // --- Step 4: Density-dependent regulation ---
                            // Promote eggs to adults up to carrying capacity
                            int EggsToBeReturned = 0;

                            if (Africa.Populations[p].Eggs.Count <= Africa.Populations[p].PopulationCapacity)
                                EggsToBeReturned = Africa.Populations[p].Eggs.Count;
                            else
                                EggsToBeReturned = Africa.Populations[p].PopulationCapacity;

                            for (int na = 0; na < EggsToBeReturned; na++)
                            {
                                Africa.Populations[p].Adults.Add(new Organism(Africa.Populations[p].Eggs[na]));
                            }

                            Africa.Populations[p].Eggs.Clear();

                            // --- Step 5: Zygotic gene drive activity ---
                            // Parentally-deposited Cas9/gRNA act on the new adults'
                            // chromosomes. HDR is severely reduced (99%) in zygotic context.
                            Africa.Populations[p].ParentalEffect(ZygoticHDRReduction);

                            #endregion

                        }


                        // --- Step 6: Inter-population migration ---
                        // After all populations have reproduced, exchange migrants
                        Africa.MigrateAll();

                    }
                }
                // === END OF SIMULATION ===

                Fwriter.Flush();
            }
        }

        //public void SimulateSweep()
        //{
        //    string pathdesktop = (string)Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        //    pathdesktop = pathdesktop + "/model";
        //    string pathString = System.IO.Path.Combine(pathdesktop, "modelsweepoutput.csv");
        //    Console.WriteLine("Writing output to: " + pathString);
        //    File.Create(pathString).Dispose();
        //    Console.WriteLine("Simulation Starts.");

        //    using (var stream = File.OpenWrite(pathString))
        //    using (var Fwriter = new StreamWriter(stream))
        //    {
        //        // THE ACTUAL SIMULATION



        //        foreach (float p0 in P0list)
        //        {
        //            Param0 = p0;

        //            foreach (float p1 in P1list)
        //            {
        //                Param1 = p1;

        //                foreach (float p2 in P2list)
        //                {
        //                    Param2 = p2;

        //                    Parallel.For(0, Iterations, i =>
        //                    {
        //                        Console.WriteLine("Iteration " + i.ToString() + " out of " + Iterations);
        //                        Console.WriteLine("Param0 = " + Param0.ToString() + " , Param1 = " + Param1.ToString() + " and Param2 = " + Param2.ToString());
        //                        Population Pop = new Population("cage setup");
        //                        for (int cGenerations = 1; cGenerations <= Generations; cGenerations++)
        //                        {
        //                            //if (ApplyIntervention)
        //                            //{
        //                            //    if ((cGenerations >= StartIntervention) && (cGenerations <= EndIntervention))
        //                            //    {
        //                            //        Pop = new Population(Pop, new Population("standard release", InterventionReleaseNumber));
        //                            //    }
        //                            //}
        //                            if (cGenerations == Generations)
        //                                Fwriter.WriteLine("{0},{1},{2},{3},{4}", i, Param0.ToString(), Param1.ToString(), Param2.ToString(), Pop.Adults.Count().ToString());
        //                            Pop.ReproduceToEggs(Mortality, PopulationCap, GlobalEggsPerFemale);
        //                            //Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Eggs", "NA", "NA", Pop.Eggs.Count.ToString(), "all");
        //                            int EggsToBeReturned = 0;
        //                            if (Pop.Eggs.Count <= PopulationCap)
        //                                EggsToBeReturned = Pop.Eggs.Count;
        //                            else
        //                                EggsToBeReturned = PopulationCap;
        //                            for (int na = 0; na < EggsToBeReturned; na++)
        //                            {
        //                                Pop.Adults.Add(new Organism(Pop.Eggs[na]));
        //                            }
        //                            Pop.Eggs.Clear();
        //                            Pop.ParentalEffect(ZygoticHDRReduction);
        //                        }
        //                    });
        //                }

        //            }
        //        }


        //        // END OF SIMULATION

        //        Fwriter.Flush();
        //    }
        //}

        //public void SimulateTimeSweep()
        //{
        //    this.Generations = 100;
        //    string pathdesktop = (string)Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        //    pathdesktop = pathdesktop + "/model";
        //    string pathString = System.IO.Path.Combine(pathdesktop, "modeltimesweepoutput.csv");
        //    Console.WriteLine("Writing output to: " + pathString);
        //    File.Create(pathString).Dispose();
        //    Console.WriteLine("Simulation Starts.");

        //    using (var stream = File.OpenWrite(pathString))
        //    using (var Fwriter = new StreamWriter(stream))
        //    {
        //        // THE ACTUAL SIMULATION



        //        foreach (float p0 in P0list)
        //        {
        //            Param0 = p0;

        //            foreach (float p1 in P1list)
        //            {
        //                Param1 = p1;

        //                foreach (float p2 in P2list)
        //                {
        //                    Param2 = p2;

        //                    Parallel.For(0, Iterations, i =>
        //                    {
        //                        Console.WriteLine("Iteration " + i.ToString() + " out of " + Iterations);
        //                        Console.WriteLine("Param0 = " + Param0.ToString() + " , Param1 = " + Param1.ToString() + " and Param2 = " + Param2.ToString());
        //                        Population Pop = new Population("cage setup");
        //                        for (int cGenerations = 1; cGenerations <= Generations; cGenerations++)
        //                        {
        //                            //if (ApplyIntervention)
        //                            //{
        //                            //    if ((cGenerations >= StartIntervention) && (cGenerations <= EndIntervention))
        //                            //    {
        //                            //        Pop = new Population(Pop, new Population("standard release", InterventionReleaseNumber));
        //                            //    }
        //                            //}
        //                            //if (cGenerations == Generations)
        //                            //    Fwriter.WriteLine("{0},{1},{2},{3},{4}", i, Param0.ToString(), Param1.ToString(), Param2.ToString(), Pop.Adults.Count().ToString());

        //                            if (Pop.Adults.Count() == 0)
        //                            {
        //                                Fwriter.WriteLine("{0},{1},{2},{3},{4}", i, Param0.ToString(), Param1.ToString(), Param2.ToString(), cGenerations.ToString());
        //                                break;
        //                            }

        //                            if (cGenerations == 100)
        //                            {
        //                                string na = "NA";
        //                                Fwriter.WriteLine("{0},{1},{2},{3},{4}", i, Param0.ToString(), Param1.ToString(), Param2.ToString(), na.ToString());
        //                                break;
        //                            }



        //                            Pop.ReproduceToEggs(Mortality, PopulationCap, GlobalEggsPerFemale);
        //                            //Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Eggs", "NA", "NA", Pop.Eggs.Count.ToString(), "all");
        //                            int EggsToBeReturned = 0;

        //                            if (Pop.Eggs.Count <= PopulationCap)
        //                                EggsToBeReturned = Pop.Eggs.Count;
        //                            else
        //                                EggsToBeReturned = PopulationCap;
        //                            for (int na = 0; na < EggsToBeReturned; na++)
        //                            {
        //                                Pop.Adults.Add(new Organism(Pop.Eggs[na]));
        //                            }
        //                            Pop.Eggs.Clear();
        //                            Pop.ParentalEffect(ZygoticHDRReduction);
        //                        }
        //                    });
        //                }

        //            }
        //        }


        //        // END OF SIMULATION

        //        Fwriter.Flush();
        //    }
        //}



    }
}
