using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.IO;


namespace SMS
{
    /// <summary>
    /// Represents a population of organisms occupying a single geographic node in the
    /// simulation. Manages two life stages:
    ///   - Adults: the current reproductive generation.
    ///   - Eggs: offspring produced by the current generation, which will become the
    ///     next generation of adults after density-dependent selection.
    ///
    /// The population has a carrying capacity (PopulationCapacity) that caps the number
    /// of eggs that survive to adulthood each generation, implementing density-dependent
    /// regulation.
    ///
    /// Key responsibilities:
    ///   - Constructing wild-type and gene drive organisms with correct genotypes.
    ///   - Performing sexual reproduction (random mating within the population).
    ///   - Applying parental effects (zygotic gene drive activity).
    ///   - Merging with other populations (for releases and migration).
    /// </summary>
    class Population
    {
        /// <summary>
        /// Maximum number of organisms that can survive to adulthood each generation.
        /// Implements density-dependent regulation: if eggs exceed this cap, only
        /// PopulationCapacity eggs are promoted to adults (first N after shuffling).
        /// </summary>
        public int PopulationCapacity;

        /// <summary>
        /// List of adult organisms in the current generation. These are the reproducing
        /// individuals. Cleared after reproduction, then refilled from eggs.
        /// </summary>
        public List<Organism> Adults
        {get;set;}

        /// <summary>
        /// List of offspring (eggs) produced by the current generation's mating events.
        /// After reproduction, eggs are promoted to adults (up to carrying capacity),
        /// and the egg list is cleared.
        /// </summary>
        public List<Organism> Eggs
        {get;set;}

        //--------------------------- Population constructors  -----------------------------------------------------


        /// <summary>
        /// Creates an empty population with no organisms and a default carrying capacity
        /// of 500. Used as a temporary container for migration transfers.
        /// </summary>
        public Population()
        {
            this.PopulationCapacity = 500;
            this.Adults = new List<Organism>();
            this.Eggs = new List<Organism>();

        }

        /// <summary>
        /// Creates a wild-type population with a 50/50 sex ratio. Generates number/2
        /// females and number/2 males, all with WT alleles at TRA, FFER, and MoY loci.
        /// The population is shuffled after creation to randomize mating order.
        /// </summary>
        /// <param name="number">Total number of organisms (split equally between sexes).
        /// Due to integer division, odd numbers will have one fewer of each sex.</param>
        /// <param name="cap">Carrying capacity for this population.</param>
        public Population(int number, int cap)
        {
            this.PopulationCapacity = cap;
            this.Adults = new List<Organism>();
            this.Eggs = new List<Organism>();

            for (int i = 0; i < number / 2; i++)
            {
                this.Adults.Add(new Organism(GenerateWTFemale()));
            }
            for (int i = 0; i < number / 2; i++)
            {
                this.Adults.Add(new Organism(GenerateWTMale()));
            }
            Shuffle.ShuffleList(this.Adults);
        }

        /// <summary>
        /// Creates a new population by merging the adults from two existing populations.
        /// All organisms are deep-cloned into the new population. Used for combining
        /// a resident population with a gene drive release population.
        /// </summary>
        /// <param name="One">First source population.</param>
        /// <param name="Two">Second source population.</param>
        /// <param name="cap">Carrying capacity for the merged population.</param>
        public Population(Population One,Population Two, int cap)
        {
            this.PopulationCapacity = cap;
            this.Adults = new List<Organism>();
            this.Eggs = new List<Organism>();

            One.Adults.ForEach((item) =>
            {
                this.Adults.Add(new Organism(item));
            });

            Two.Adults.ForEach((item) =>
            {
                this.Adults.Add(new Organism(item));
            });

            Shuffle.ShuffleList(this.Adults);

        }


        /// <summary>
        /// Creates a gene drive release population containing only drive males.
        /// Each organism is a male heterozygous for the TRA gene drive transgene
        /// (one copy Transgene, one copy WT) carrying Cas9 and gRNA_TRA.
        /// Sets a high carrying capacity (10000) since this is a release cohort,
        /// not a self-sustaining population.
        /// </summary>
        /// <param name="number">Number of gene drive males to create.</param>
        public Population(int number)
        {
            this.PopulationCapacity = 10000;
            this.Adults = new List<Organism>();
            this.Eggs = new List<Organism>();


                for (int u = 0; u < number; u++)
                {

                    Organism D_Male = new Organism(Generate_DriveMale());
                    this.Adults.Add(D_Male);

                }

            Shuffle.ShuffleList(this.Adults);
        }

        //---------------------- Define Organism Types -----------------------------------------------------


        /// <summary>
        /// Constructs a wild-type female organism with the following genome:
        ///
        /// Chromosome layout (3 homologous pairs):
        ///   Pair "Sex": X / X  (empty, no loci — sex determined by absence of MoY)
        ///   Pair "2":   FFER(WT) / FFER(WT)  — on chromosome 2, position 1.0
        ///   Pair "3":   TRA(WT)  / TRA(WT)   — on chromosome 3, position 2.0
        ///
        /// Each WT locus carries traits parameterized by Simulation.Param0/Param2:
        ///   - Conservation (Param2): probability that NHEJ produces R2 vs R1
        ///   - HomRepair_male/female (Param0): HDR efficiency (used by the drive
        ///     Transgene on the homologous chromosome, not by WT itself, but set here
        ///     so the trait exists at the locus for consistency)
        ///
        /// Parental factors: TRA_mRNA = 1 (maternal TRA provision for sex determination)
        ///
        /// Note: FFER locus is included but noted as "not really needed for TRA sim only"
        /// — it serves as a second gene drive target for multi-locus drive designs.
        /// </summary>
        /// <returns>A new wild-type XX female organism.</returns>
        public Organism GenerateWTFemale()
        {
            Organism WTFemale = new Organism();

            //ffer not really needed for tra sim only
            GeneLocus FFERa = new GeneLocus("FFER", 1F, "WT");
            FFERa.AddToTraits("Conservation", Simulation.Param2);
            FFERa.AddToTraits("HomRepair_male", Simulation.Param0);
            FFERa.AddToTraits("HomRepair_female", Simulation.Param0);
            GeneLocus FFERb = new GeneLocus("FFER", 1F, "WT");
            FFERb.AddToTraits("Conservation", Simulation.Param2);
            FFERb.AddToTraits("HomRepair_male", Simulation.Param0);
            FFERb.AddToTraits("HomRepair_female", Simulation.Param0);

            GeneLocus TRAa = new GeneLocus("TRA", 2F, "WT");
            TRAa.AddToTraits("Conservation", Simulation.Param2);
            TRAa.AddToTraits("HomRepair_male", Simulation.Param0);
            TRAa.AddToTraits("HomRepair_female", Simulation.Param0);
            GeneLocus TRAb = new GeneLocus("TRA", 2F, "WT");
            TRAb.AddToTraits("Conservation", Simulation.Param2);
            TRAb.AddToTraits("HomRepair_male", Simulation.Param0);
            TRAb.AddToTraits("HomRepair_female", Simulation.Param0);

            // Build chromosome pairs: Sex (X/X), autosome 2 (FFER), autosome 3 (TRA)
            Chromosome ChromXa = new Chromosome("X", "Sex");
            Chromosome ChromXb = new Chromosome("X", "Sex");
            Chromosome Chrom2a = new Chromosome("2", "2");
            Chromosome Chrom2b = new Chromosome("2", "2");
            Chromosome Chrom3a = new Chromosome("3", "3");
            Chromosome Chrom3b = new Chromosome("3", "3");

            Chrom2a.GeneLocusList.Add(FFERa);
            Chrom2b.GeneLocusList.Add(FFERb);

            Chrom3a.GeneLocusList.Add(TRAa);
            Chrom3b.GeneLocusList.Add(TRAb);

            // Assemble diploid genome: ListA and ListB must have chromosomes at
            // matching indices for homologous pairing
            WTFemale.ChromosomeListA.Add(ChromXa);
            WTFemale.ChromosomeListB.Add(ChromXb);
            WTFemale.ChromosomeListA.Add(Chrom2a);
            WTFemale.ChromosomeListB.Add(Chrom2b);
            WTFemale.ChromosomeListA.Add(Chrom3a);
            WTFemale.ChromosomeListB.Add(Chrom3b);

            // Set maternal TRA mRNA provision (WT females always provide TRA mRNA)
            WTFemale.AddToParentalFactors("TRA_mRNA", 1F);

            return WTFemale;
        }

        /// <summary>
        /// Constructs a wild-type male by cloning a WT female and replacing one X
        /// chromosome (ChromosomeListA[0]) with a Y chromosome carrying the MoY
        /// (Maleness-on-Y) gene. This gives the male:
        ///   Pair "Sex": Y(MoY=WT) / X  — XY karyotype
        ///   Pair "2":   FFER(WT) / FFER(WT)
        ///   Pair "3":   TRA(WT)  / TRA(WT)
        /// </summary>
        /// <returns>A new wild-type XY male organism.</returns>
        public Organism GenerateWTMale()
        {
            Organism WTMale = new Organism(GenerateWTFemale());
            Chromosome ChromY = new Chromosome("Y", "Sex");
            GeneLocus MaleFactor = new GeneLocus("MoY", 1F, "WT");
            ChromY.GeneLocusList.Add(MaleFactor);

            // Replace the first X (in ListA) with the Y chromosome
            WTMale.ChromosomeListA[0] = ChromY;

            return WTMale;
        }

        /// <summary>
        /// Constructs a gene drive male by cloning a WT male and replacing ONE copy
        /// of the TRA gene (on ChromosomeListA) with the Transgene allele. The resulting
        /// organism is hemizygous for the drive: TRA(Transgene)/TRA(WT).
        ///
        /// The Transgene TRA locus carries the following traits:
        ///   - Cas9_male (Param1):    Cas9 activity in male germline
        ///   - Cas9_female (Param1):  Cas9 activity in female germline
        ///   - Cas9_maternal (Param1): Maternal Cas9 deposition into embryo
        ///   - Cas9_paternal (0):      No paternal Cas9 deposition
        ///   - gRNA_TRA (1.0):         Full gRNA expression targeting TRA
        ///   - HomRepair_male (Param0): HDR rate in males
        ///   - HomRepair_female (Param0): HDR rate in females
        ///
        /// This configuration means:
        ///   - The drive actively cuts WT TRA alleles in both male and female germlines.
        ///   - Cas9 is maternally but not paternally deposited into embryos.
        ///   - gRNA is always expressed at full level (1.0).
        ///   - Only TRA is directly targeted (FFER would need separate gRNA).
        /// </summary>
        /// <returns>A new gene drive XY male organism, heterozygous TRA(Transgene)/TRA(WT).</returns>
        public Organism Generate_DriveMale()
        {
            Organism D_Male = new Organism(GenerateWTMale());

            GeneLocus TRADRIVE = new GeneLocus("TRA", 2F, "Transgene");
            TRADRIVE.AddToTraits("Cas9_male", Simulation.Param1);
            TRADRIVE.AddToTraits("Cas9_female", Simulation.Param1);
            TRADRIVE.AddToTraits("Cas9_maternal", Simulation.Param1);
            TRADRIVE.AddToTraits("Cas9_paternal", 0F);
            TRADRIVE.AddToTraits("gRNA_TRA", 1F);
            TRADRIVE.AddToTraits("HomRepair_male", Simulation.Param0);
            TRADRIVE.AddToTraits("HomRepair_female", Simulation.Param0);

            // Replace WT TRA allele on ChromosomeListA with the Transgene
            D_Male.ModifyAllele("A", TRADRIVE, "WT");
            return D_Male;
        }


       

        //----------------------- Population methods ----------------------------------------------------


        /// <summary>
        /// Performs a single mating cross between a male and female organism, producing
        /// a list of offspring (eggs). The number of eggs is the base fecundity
        /// (GlobalEggsPerFemale) multiplied by both parents' fertility values.
        /// Each egg is created via the sexual reproduction constructor Organism(Dad, Mum),
        /// which performs meiosis, gene drive, and parental factor determination.
        /// </summary>
        /// <param name="Dad">The paternal organism (must be male).</param>
        /// <param name="Mum">The maternal organism (must be female).</param>
        /// <param name="GlobalEggsPerFemale">Base number of eggs per cross.</param>
        /// <returns>List of offspring organisms.</returns>
        public List<Organism> PerformCross(Organism Dad, Organism Mum, int GlobalEggsPerFemale)
        {
            int EggsPerFemale = GlobalEggsPerFemale;
            List<Organism> EggList = new List<Organism>();

            // Adjust egg count by parental fertility (currently always 1.0)
            EggsPerFemale = (int)(EggsPerFemale * Dad.GetFertility() * Mum.GetFertility());

            for (int i = 0; i < EggsPerFemale; i++)
            {
                EggList.Add(new Organism(Dad, Mum));
            }

            return EggList;
        }

        /// <summary>
        /// Executes one generation of reproduction for this population. Each female
        /// in the population attempts to mate with a randomly selected male.
        ///
        /// Algorithm:
        ///   1. Shuffle adults to randomize iteration order.
        ///   2. Compute EffectivePopulation = (1 - mortality) * cap. This limits how
        ///      many random mate-search attempts each female gets — if no male is found
        ///      in that many tries, the female does not reproduce.
        ///   3. For each female, repeatedly pick a random adult. If it's male, perform
        ///      the cross and stop searching (monogamous per generation). This means
        ///      males can mate with multiple females (polygyny).
        ///   4. Clear all adults (they die) and shuffle the eggs.
        ///
        /// The mortality parameter (m) effectively controls the mate-finding probability:
        /// higher mortality → fewer search attempts → higher chance females fail to find
        /// a mate, reducing effective fecundity.
        /// </summary>
        /// <param name="m">Mortality rate (0–1). Reduces the number of mate-search attempts.</param>
        /// <param name="cap">Carrying capacity (used to scale mate-search attempts).</param>
        /// <param name="GlobalEggsPerFemale">Base fecundity (eggs per successful mating).</param>
        public void ReproduceToEggs(float m,int cap, int GlobalEggsPerFemale)
        {
            Shuffle.ShuffleList(this.Adults);

            // EffectivePopulation controls how many random mate-search attempts each female gets
            int EffectivePopulation = (int)((1 - m) * cap);

            int numb;
            foreach (Organism F1 in this.Adults)
            {
                if (F1.GetSex() == "male")
                {
                    continue;  // Skip males; only females initiate mating
                }
                else
                {
                    // Try up to EffectivePopulation random picks to find a male
                    for (int a = 0; a < EffectivePopulation; a++)
                    {
                        numb = Shuffle.random.Next(0, this.Adults.Count);
                        if (this.Adults[numb].GetSex() == "male")
                        {
                            // Found a male — mate and produce eggs
                            this.Eggs.AddRange(this.PerformCross(this.Adults[numb], F1, GlobalEggsPerFemale));
                            break;
                        }
                    }
                }

            }

            // All adults die after reproduction (non-overlapping generations)
            this.Adults.Clear();
            Shuffle.ShuffleList(this.Eggs);

        }

        /// <summary>
        /// Applies post-fertilization (zygotic/embryonic) effects to all adults in the
        /// population. Called after eggs have been promoted to adults.
        ///
        /// For each organism:
        ///   1. With 50% probability, attempt to swap ChromosomeListA and ListB
        ///      (randomizes which parental chromosome set is in which list). Note: this
        ///      swap has a bug in SwapChromLists and does not actually work.
        ///   2. Apply zygotic Cas9 activity: parentally-deposited Cas9/gRNA act on the
        ///      organism's own chromosomes to convert remaining WT alleles. The
        ///      ZygoticHDRReduction parameter (default 0.99) severely limits HDR in
        ///      the zygotic context, making NHEJ (resistance allele formation) the
        ///      dominant outcome of zygotic cutting.
        /// </summary>
        /// <param name="ZygoticHDRReduction">HDR efficiency reduction factor for zygotic context.</param>
        public void ParentalEffect(float ZygoticHDRReduction)
        {
            foreach (Organism OM in this.Adults)
            {

                if (Shuffle.random.Next(0, 2) != 0)
                {
                    OM.SwapChromLists();
                }

                OM.ZygoticCas9Activity(ZygoticHDRReduction);

            }

        }

        /// <summary>
        /// Merges another population's adults into this population by deep-cloning each
        /// organism. The combined population is then shuffled to randomize mating order.
        /// Used for:
        ///   - Gene drive release events (adding release males to a wild population)
        ///   - Migration (adding migrants from another population)
        /// </summary>
        /// <param name="Two">The source population whose adults will be cloned and added.</param>
        public void AddToPopulation(Population Two)
        {

            Two.Adults.ForEach((item) =>
            {
                this.Adults.Add(new Organism(item));
            });


            Shuffle.ShuffleList(this.Adults);

        }

    }

}
