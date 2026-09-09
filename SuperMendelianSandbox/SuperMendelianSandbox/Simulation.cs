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
    /// Three genetic control systems are implemented, selected by <see cref="Model"/>:
    ///
    ///   "ffer"   — Suppressive CRISPR homing drive targeting a female fertility gene
    ///              in Anopheles. Heterozygous carriers convert WT alleles to drive
    ///              copies in the germline (HDR) or to resistance alleles (NHEJ);
    ///              females homozygous for a disrupted allele are sterile, so the
    ///              accumulating genetic load suppresses the population.
    ///
    ///   "ydrive" — Y-linked X-shredder sex distorter (a driving Y chromosome).
    ///              A nuclease carried on the Y destroys X-bearing gametes during male
    ///              meiosis, so drive males sire almost exclusively sons. The Y spreads
    ///              because it is over-transmitted, and the population collapses as
    ///              females disappear.
    ///
    ///   "medea"  — Maternal-Effect Dominant Embryonic Arrest element. Mothers carrying
    ///              the element load every egg with a toxin; only embryos that inherit
    ///              a linked zygotic rescue survive. Non-carrier offspring of carrier
    ///              mothers are removed, so the element spreads to fixation without
    ///              suppressing the population — a population MODIFICATION system.
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

        /// <summary>Which genetic control system to simulate: "ffer" (suppressive
        /// female-fertility homing drive), "ydrive" (Y-linked X-shredder sex distorter)
        /// or "medea" (maternal-effect toxin/zygotic-rescue element).
        /// Static because Population, Organism and Chromosome all branch on it while
        /// building genomes and resolving inheritance.
        /// Set from the web configuration page.</summary>
        public static string Model = "ffer";

        /// <summary>Number of discrete, non-overlapping generations to simulate.</summary>
        public int Generations = 30;

        /// <summary>Number of independent replicate runs. Each iteration starts with a
        /// fresh environment to capture stochastic variation.</summary>
        public int Iterations = 3;

        /// <summary>Per-generation natural mortality rate (0-1). Controls the number of
        /// mate-finding attempts in ReproduceToEggs: EffectivePopulation = (1-Mortality)*cap.
        /// Higher mortality means harder to find mates and lower effective reproduction.</summary>
        public float Mortality = 0.1f;

        /// <summary>Fitness cost of carrying the transgene, charged as additional
        /// per-generation mortality among adults before they reproduce (0-1). Applies
        /// to all three models. The cost is per insertion, so a heterozygote survives
        /// with probability (1 - cost) and a homozygote with (1 - cost)^2.
        ///
        /// This is the only parameter that removes transgenic alleles from the
        /// population irrespective of the drive mechanism, and it is therefore what
        /// determines whether a threshold-dependent system can invade from a given
        /// release size. Set from web configuration page.</summary>
        public float TransgeneFitnessCost = 0.05F;

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

        /*--- Driving Y / X-shredder parameters (Model == "ydrive") ------------------*/

        /// <summary>Fraction of X-bearing gametes destroyed by the Y-linked shredder
        /// during male meiosis (0-1). Carried as the "X_shred" trait on the YLD
        /// Transgene allele. The surviving gamete pool is (1 - rate) X : 1 Y, so the
        /// probability that a sperm carries the Y is 1 / (2 - rate): 0.5 at rate 0
        /// (Mendelian) rising to 1.0 at rate 1 (all-male progeny).
        /// Set from web configuration page.</summary>
        public static float XShredRate = 0.95F;

        /// <summary>Fertility of a male carrying the driving Y, as a multiplier on the
        /// number of eggs his mate produces (0-1). Folds together the loss of half the
        /// sperm complement to shredding and any fitness cost of the construct itself.
        /// Set from web configuration page.</summary>
        public static float YDriveFertility = 0.9F;

        /*--- MEDEA parameters (Model == "medea") -----------------------------------*/

        /// <summary>Penetrance of the maternal-effect toxin (0-1): the probability that
        /// an embryo receiving toxin but no rescue actually arrests. Carried as the
        /// "Medea_toxin" trait on the MTOX Transgene allele. Values below 1 make the
        /// element leaky, letting non-carriers escape and slowing its spread.
        /// Set from web configuration page.</summary>
        public static float MedeaPenetrance = 0.95F;

        /// <summary>Efficiency of the zygotic rescue (0-1): the probability that an
        /// embryo inheriting a rescue copy survives a toxin-loaded egg. Carried as the
        /// "Medea_rescue" trait on the MRES Transgene allele. Values below 1 kill some
        /// carriers too, which is the element's fitness cost and the source of its
        /// release-frequency threshold. Set from web configuration page.</summary>
        public static float MedeaRescue = 0.95F;

        /// <summary>Map distance between the toxin (MTOX) and rescue (MRES) loci, in
        /// map units (0-0.5). Recombination frequency between them is min(distance, 0.5),
        /// so at 0 the two halves are perfectly linked and travel as one element, while
        /// larger values let crossovers separate them — producing rescue-only chromosomes
        /// that are immune to the toxin without paying to make it. Those free riders
        /// dilute the intact element and break the drive.
        /// Set from web configuration page.</summary>
        public static float MedeaDistance = 0.0F;

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

        /// <summary>Defines which gRNA targets which gene. Each row is {target_gene, gRNA_name}.
        /// The gene drive's Cas9 uses each gRNA to cut the corresponding target gene.
        /// Only the CRISPR homing drive ("ffer") uses guides; the driving Y and MEDEA
        /// carry no nuclease/guide pair, so for those models this is left empty and every
        /// cut-and-home loop in Chromosome and Organism becomes a no-op.
        /// This is a static field accessed throughout the simulation by Chromosome and
        /// Organism classes during gene drive mechanics.</summary>
        public static string[,] Target_cognate_gRNA = { { "FFER", "gRNA_FFER" }, { "TRA", "gRNA_TRA" } };

        /// <summary>Selects the gRNA/target table for the active model. Called from
        /// ApplyConfig once the model is known, before any organism is built.</summary>
        private static void ConfigureGuides()
        {
            if (Model == "ffer")
                Target_cognate_gRNA = new string[,] { { "FFER", "gRNA_FFER" }, { "TRA", "gRNA_TRA" } };
            else
                Target_cognate_gRNA = new string[0, 2];
        }

        /// <summary>Names of the genes whose genotype frequencies are written to the
        /// output for the active model. MEDEA tracks both halves of the element so that
        /// their frequencies can be compared — a rescue frequency running ahead of the
        /// toxin frequency is the signature of recombinational uncoupling.</summary>
        private string[] TrackedGenes()
        {
            switch (Model)
            {
                case "ydrive": return new[] { "YLD" };
                case "medea":  return new[] { "MTOX", "MRES" };
                default:       return new[] { "FFER" };
            }
        }

        /*------------------------------- The Simulation ---------------------------------------------*/

        /// <summary>
        /// Main simulation method. Runs the complete multi-iteration, multi-generation
        /// simulation for the active model and writes all output to CSV.
        ///
        /// Output file: OutputDir/modeloutput.csv, where OutputDir defaults to
        /// ./output/ and is set to output/&lt;model&gt;/ when launched from the web
        /// configuration page, so each model's results are kept separate.
        ///
        /// CSV columns (with header row):
        ///   Iteration, Environ, Population, Generation, Category, Value1, Value2, Count, Type
        ///
        /// Output categories per population per generation:
        ///   - Genotype frequencies for the model's tracked loci (see TrackedGenes) --
        ///     both "all" (full census) and "sample" (first N=48 organisms, simulating
        ///     field sampling).
        ///   - Phenotypic sex counts (Males, Females) -- "all" census.
        ///   - Karyotype counts (XX, XY) -- "all" census.
        ///   - Viable egg count produced -- "all".
        ///
        /// Simulation flow per generation per population:
        ///   1. Apply intervention: if enabled and within the intervention window, release
        ///      InterventionReleaseNumber transgenic males into population 0.
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
        ///   chain with migration decaying 10x per step from MigrationBaseRate:
        ///     Pop 0-1: base,  1-2: base/10,  2-3: base/100,  3-4: base/1000
        ///   Transgenic males are released into population 0.
        /// </summary>
        public void Simulate()
        {
            Directory.CreateDirectory(OutputDir);

            string pathString = Path.Combine(OutputDir, "modeloutput.csv");
            string statusPath = Path.Combine(OutputDir, "simstatus.json");

            Console.WriteLine("Writing output to: " + pathString);
            File.Create(pathString).Dispose();

            Console.WriteLine("Simulation Starts. Model = " + Model);
            WriteStatus(statusPath, "running", 0, 0);

            string[] Track = TrackedGenes();

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

                            // Transgene fitness cost, charged before reproduction so the
                            // census below counts only the adults that actually breed.
                            // Released males pay it too, like any other carrier.
                            Africa.Populations[p].ApplyTransgeneFitnessCost(TransgeneFitnessCost);

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

            // The model must be set before anything else, since it decides which
            // genome layout and inheritance rules the rest of the run uses.
            if (root.TryGetProperty("model", out var mod))
                Model = mod.GetString() ?? Model;
            ConfigureGuides();

            if (root.TryGetProperty("generations", out var gen))
                Generations = gen.GetInt32();
            if (root.TryGetProperty("releaseNumber", out var rel))
                InterventionReleaseNumber = rel.GetInt32();
            if (root.TryGetProperty("mortality", out var mort))
                Mortality = mort.GetSingle();
            if (root.TryGetProperty("fitnessCost", out var fit))
                TransgeneFitnessCost = fit.GetSingle();
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

            // Driving Y / X-shredder
            if (root.TryGetProperty("xShredRate", out var shred))
                XShredRate = shred.GetSingle();
            if (root.TryGetProperty("yDriveFertility", out var yfer))
                YDriveFertility = yfer.GetSingle();

            // MEDEA
            if (root.TryGetProperty("medeaPenetrance", out var pen))
                MedeaPenetrance = pen.GetSingle();
            if (root.TryGetProperty("medeaRescue", out var res))
                MedeaRescue = res.GetSingle();
            if (root.TryGetProperty("medeaDistance", out var dist))
                MedeaDistance = dist.GetSingle();

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
