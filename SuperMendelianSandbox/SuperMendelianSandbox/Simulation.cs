using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
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
    ///   - Each iteration creates a fresh metapopulation environment (Environ).
    ///   - Each generation: apply intervention, record data, reproduce, regulate,
    ///     apply zygotic effects, migrate.
    ///   - Output: genotype frequencies, sex ratios, karyotypes, and egg counts per
    ///     environment per population per generation, written to CSV.
    /// </summary>
    class Simulation
    {

        /*-------------------- Simulation Parameters ---------------------------------*/

        /// <summary>Number of discrete, non-overlapping generations to simulate.</summary>
        public int Generations = 30;

        /// <summary>Number of independent replicate runs. Each iteration starts with a
        /// fresh environment to capture stochastic variation.</summary>
        public int Iterations = 3;

        /// <summary>Per-generation natural mortality rate (0-1). Controls the number of
        /// mate-finding attempts in ReproduceToEggs: EffectivePopulation = (1-Mortality)*cap.
        /// Higher mortality means harder to find mates and lower effective reproduction.</summary>
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
        public int StartIntervention = 3;

        /// <summary>Last generation in which gene drive males are released (inclusive).
        /// When equal to StartIntervention, release occurs in a single generation.</summary>
        public int EndIntervention = 3;

        /// <summary>Number of gene drive males released per intervention generation
        /// into population 0.</summary>
        public int InterventionReleaseNumber = 100;

        /// <summary>Param0: Homology-directed repair (HDR) efficiency (0-1).
        /// Used as the "HomRepair_male" and "HomRepair_female" trait value for both
        /// WT target loci and the Transgene. Default 0.95 = 95% HDR success when Cas9 cuts.</summary>
        public static float Param0 = 0.97F;

        /// <summary>Param1: Cas9 nuclease activity level (0-1).
        /// Used as the "Cas9_male", "Cas9_female", and "Cas9_maternal" trait values
        /// for the Transgene. Default 0.95 = 95% cutting probability.</summary>
        public static float Param1 = 0.97F;

        /// <summary>Param2: Conservation level (0-1).
        /// Used as the "Conservation" trait at WT loci. Determines the probability that
        /// NHEJ repair at a cut site produces an R2 (loss-of-function, non-functional
        /// resistance) allele vs an R1 (functional resistance) allele.
        /// Default 1.0 = no functional resistance (R1) can arise.</summary>
        public static float Param2 = 1.0F;


        /// <summary>Param3: Maternal Cas9 deposition level (0-1).
        /// Separate from germline Cas9 activity (Param1) to allow independent tuning
        /// of maternal vs germline drive. Set from web configuration page.</summary>
        public static float Param3 = 0.1F;

        /// <summary>Directory for simulation output files (CSV, status JSON).
        /// Defaults to ./output/ relative to the working directory. Overridden
        /// by the "outputDir" field in the JSON config file when launched from
        /// the web configuration page.</summary>
        public string OutputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");

        /// <summary>Base migration rate for the linear population chain.
        /// Each successive population pair gets 10x lower rate:
        ///   Pop 0-1: base, Pop 1-2: base/10, Pop 2-3: base/100, Pop 3-4: base/1000.
        /// Set from web configuration page.</summary>
        public float MigrationBaseRate = 0.1F;

        /// <summary>Names of genes whose genotype frequencies are tracked in the output.
        /// TRA is the primary gene drive target; FFER is a secondary target locus.</summary>
        //string[] Track = {"TRA","FFER"};

        /// <summary>Names of genes whose genotype frequencies are tracked in the output.
        /// FFER is the target locus, TRA ignored in this configuration.</summary>
        string[] Track = {"FFER"};


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
        /// CSV columns (with header row):
        ///   Iteration, Environ, Population, Generation, Category, Value1, Value2, Count, Type
        ///
        /// Output categories per population per generation:
        ///   - Genotype frequencies for tracked genes (TRA, FFER) -- both "all" (full census)
        ///     and "sample" (first N=48 organisms, simulating field sampling).
        ///   - Phenotypic sex counts (Males, Females) -- "all" census.
        ///   - Karyotype counts (XX, XY) -- "all" census.
        ///   - Total egg count produced -- "all".
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
        ///   5 populations of 500 individuals each (cap 500), connected in a linear
        ///   chain with exponentially decreasing migration rates:
        ///     Pop 0-1: 10%,  1-2: 1%,  2-3: 0.1%,  3-4: 0.01%
        ///   Gene drive males are released into population 0.
        /// </summary>
        public void Simulate()
        {
            Directory.CreateDirectory(OutputDir);

            string pathString = Path.Combine(OutputDir, "modeloutput.csv");
            string statusPath = Path.Combine(OutputDir, "simstatus.json");

            Console.WriteLine("Writing output to: " + pathString);
            File.Create(pathString).Dispose();

            Console.WriteLine("Simulation Starts.");
            WriteStatus(statusPath, "running", 0, 0);

            using (var stream = File.OpenWrite(pathString))
            using (var Fwriter = new StreamWriter(stream))
            {
                Fwriter.WriteLine("Iteration,Environ,Population,Generation,Category,Value1,Value2,Count,Type");

                for (int cIterations = 1; cIterations <= Iterations; cIterations++)
                {
                    Console.WriteLine("Iteration " + cIterations + " out of " + Iterations);

                    // --- Parameters set from web configuration page ---
                    Environ Africa = new Environ("Africa", 5, 500, 500);

                    // Linear chain migration with 10x decay per step from base rate
                    float migRate = MigrationBaseRate;
                    for (int m = 0; m < 4; m++)
                    {
                        Africa.DefineMigration(m, m + 1, migRate);
                        migRate /= 10F;
                    }

                    for (int cGenerations = 1; cGenerations <= Generations; cGenerations++)
                    {
                        for (var p = 0; p < Africa.Populations.Count; p++)
                        {
                            if (ApplyIntervention)
                            {
                                if ((cGenerations >= StartIntervention) && (cGenerations <= EndIntervention))
                                {
                                    if (p == 0)
                                    {
                                        Population Release = new Population(InterventionReleaseNumber);
                                        Africa.Populations[p].AddToPopulation(Release);
                                    }
                                }
                            }

                            #region Output adult data to file

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
                                Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},all", cIterations, Africa.Name, p, cGenerations, result.Name, result.Count);
                            }

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
                                Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},sample", cIterations, Africa.Name, p, cGenerations, result.Name, result.Count);
                            }

                            int numberofallmales = 0;
                            int numberofallfemales = 0;
                            foreach (Organism O in Africa.Populations[p].Adults)
                            {
                                if (O.GetSex() == "female")
                                    numberofallfemales++;
                                else
                                    numberofallmales++;
                            }
                            Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}", cIterations, Africa.Name, p, cGenerations, "Sex", "Males", "NA", numberofallmales, "all");
                            Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}", cIterations, Africa.Name, p, cGenerations, "Sex", "Females", "NA", numberofallfemales, "all");

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
                            Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}", cIterations, Africa.Name, p, cGenerations, "Sex_Karyotype", "XX", "NA", numberofXX, "all");
                            Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}", cIterations, Africa.Name, p, cGenerations, "Sex_Karyotype", "XY", "NA", numberofXY, "all");

                            #endregion

                            #region Cross all adults and return eggs for next generation

                            Africa.Populations[p].ReproduceToEggs(Mortality, Africa.Populations[p].PopulationCapacity, GlobalEggsPerFemale);

                            Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}", cIterations, Africa.Name, p, cGenerations, "Eggs", "NA", "NA", Africa.Populations[p].Eggs.Count.ToString(), "all");

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

                            Africa.Populations[p].ParentalEffect(ZygoticHDRReduction);

                            #endregion

                        }

                        Africa.MigrateAll();

                        WriteStatus(statusPath, "running", cIterations, cGenerations);
                    }
                }

                Fwriter.Flush();
            }

            WriteStatus(statusPath, "completed", Iterations, Generations);
        }

        /// <summary>
        /// Applies configuration from a JSON string. Called from Program.cs when a
        /// config file path is passed as a command-line argument.
        /// All configurable parameters are set here from the web configuration page.
        /// </summary>
        public void ApplyConfig(string json)
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("generations", out var gen))
                Generations = gen.GetInt32();
            if (root.TryGetProperty("releaseNumber", out var rel))
                InterventionReleaseNumber = rel.GetInt32();
            if (root.TryGetProperty("mortality", out var mort))
                Mortality = mort.GetSingle();
            if (root.TryGetProperty("eggsPerFemale", out var eggs))
                GlobalEggsPerFemale = eggs.GetInt32();
            if (root.TryGetProperty("cas9Activity", out var cas9))
                Param1 = cas9.GetSingle();
            if (root.TryGetProperty("hdrRate", out var hdr))
                Param0 = hdr.GetSingle();
            if (root.TryGetProperty("conservation", out var cons))
                Param2 = cons.GetSingle();
            if (root.TryGetProperty("maternalCas9", out var mat))
                Param3 = mat.GetSingle();
            if (root.TryGetProperty("migrationBaseRate", out var mig))
                MigrationBaseRate = mig.GetSingle();
            if (root.TryGetProperty("outputDir", out var outDir))
                OutputDir = outDir.GetString() ?? OutputDir;
        }

        /// <summary>
        /// Writes simulation progress to a JSON status file, polled by the web
        /// configuration page to update the progress bar.
        /// </summary>
        private void WriteStatus(string path, string status, int iteration, int generation)
        {
            string json = JsonSerializer.Serialize(new
            {
                status,
                iteration,
                totalIterations = Iterations,
                generation,
                totalGenerations = Generations
            });
            File.WriteAllText(path, json);
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
