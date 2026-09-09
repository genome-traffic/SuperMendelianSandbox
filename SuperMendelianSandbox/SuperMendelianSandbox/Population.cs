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
        /// Creates a release population containing only transgenic males, of whichever
        /// construct the active model calls for (see Generate_DriveMale).
        /// Sets a high carrying capacity (10000) since this is a release cohort,
        /// not a self-sustaining population.
        /// </summary>
        /// <param name="number">Number of transgenic males to create.</param>
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


        //---------------------- Genome layout constants -----------------------------

        /// <summary>Map position of the drive target locus on autosome 2. Also the
        /// position of the MEDEA toxin half; the rescue sits Simulation.MedeaDistance
        /// further along.</summary>
        const float TargetPosition = 1F;

        /// <summary>Map position of the TRA locus on autosome 3. TRA is retained in
        /// every model because Organism.GetSex reads maternal TRA provision, but only
        /// the "ffer" model ever carries a guide able to cut it.</summary>
        const float TraPosition = 2F;

        /// <summary>Map position of the Y-linked distorter insertion site. The same
        /// site is scored on the X so that the construct's spread can be read as an
        /// ordinary allele frequency; the sex pair never recombines, so the locus
        /// cannot actually move between an X and a Y.</summary>
        const float DistorterPosition = 2F;

        /// <summary>
        /// Builds a wild-type gene locus with the repair and conservation traits that
        /// the CRISPR machinery reads. These traits are inert in the models that carry
        /// no nuclease, but are set everywhere so every locus has a complete trait set.
        /// </summary>
        GeneLocus WTLocus(string gene, float position)
        {
            GeneLocus L = new GeneLocus(gene, position, "WT");
            L.AddToTraits("Conservation", Simulation.Param2);
            L.AddToTraits("HomRepair_male", Simulation.Param0);
            L.AddToTraits("HomRepair_female", Simulation.Param0);
            return L;
        }

        /// <summary>
        /// Adds the wild-type loci that autosome 2 carries under the active model.
        ///
        ///   "medea" — MTOX (toxin) and MRES (rescue), separated by
        ///             Simulation.MedeaDistance map units so that crossover between
        ///             them can uncouple the two halves of the element.
        ///   others  — FFER, the single female fertility target locus.
        /// </summary>
        void AddChrom2Loci(Chromosome Chrom)
        {
            if (Simulation.Model == "medea")
            {
                Chrom.GeneLocusList.Add(WTLocus("MTOX", TargetPosition));
                Chrom.GeneLocusList.Add(WTLocus("MRES", TargetPosition + Simulation.MedeaDistance));
            }
            else
            {
                Chrom.GeneLocusList.Add(WTLocus("FFER", TargetPosition));
            }
        }

        /// <summary>
        /// Constructs a wild-type female organism. The genome has three homologous
        /// pairs; what sits on autosome 2 and on the sex chromosomes depends on the
        /// model being simulated:
        ///
        ///   Pair "Sex": X / X. Empty in the "ffer" and "medea" models — femaleness
        ///               follows from the absence of MoY. In "ydrive" each X also
        ///               carries YLD(WT), the wild-type version of the distorter
        ///               insertion site, so the construct's frequency can be scored.
        ///   Pair "2":   The drive target: FFER, or MTOX + MRES under MEDEA.
        ///   Pair "3":   TRA(WT) / TRA(WT), retained in all models for sex determination.
        ///
        /// Parental factors: TRA_mRNA = 1 (maternal TRA provision for sex determination).
        /// </summary>
        /// <returns>A new wild-type XX female organism.</returns>
        public Organism GenerateWTFemale()
        {
            Organism WTFemale = new Organism();

            Chromosome ChromXa = new Chromosome("X", "Sex");
            Chromosome ChromXb = new Chromosome("X", "Sex");
            Chromosome Chrom2a = new Chromosome("2", "2");
            Chromosome Chrom2b = new Chromosome("2", "2");
            Chromosome Chrom3a = new Chromosome("3", "3");
            Chromosome Chrom3b = new Chromosome("3", "3");

            // The distorter insertion site is scored on both sex chromosomes so that
            // wild-type and drive Y chromosomes are distinguishable in the output.
            if (Simulation.Model == "ydrive")
            {
                ChromXa.GeneLocusList.Add(WTLocus("YLD", DistorterPosition));
                ChromXb.GeneLocusList.Add(WTLocus("YLD", DistorterPosition));
            }

            AddChrom2Loci(Chrom2a);
            AddChrom2Loci(Chrom2b);

            Chrom3a.GeneLocusList.Add(WTLocus("TRA", TraPosition));
            Chrom3b.GeneLocusList.Add(WTLocus("TRA", TraPosition));

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
        /// (Maleness-on-Y) gene. Under the "ydrive" model the Y also carries YLD(WT),
        /// marking an unmodified distorter insertion site.
        /// </summary>
        /// <returns>A new wild-type XY male organism.</returns>
        public Organism GenerateWTMale()
        {
            Organism WTMale = new Organism(GenerateWTFemale());
            Chromosome ChromY = new Chromosome("Y", "Sex");
            GeneLocus MaleFactor = new GeneLocus("MoY", 1F, "WT");
            ChromY.GeneLocusList.Add(MaleFactor);

            if (Simulation.Model == "ydrive")
                ChromY.GeneLocusList.Add(WTLocus("YLD", DistorterPosition));

            // Replace the first X (in ListA) with the Y chromosome
            WTMale.ChromosomeListA[0] = ChromY;

            return WTMale;
        }

        /// <summary>
        /// Constructs the transgenic male that is released into the population,
        /// dispatching to the builder for the active model.
        /// </summary>
        /// <returns>A new transgenic XY male organism.</returns>
        public Organism Generate_DriveMale()
        {
            switch (Simulation.Model)
            {
                case "ydrive": return Generate_YDriveMale();
                case "medea":  return Generate_MedeaMale();
                default:       return Generate_HomingDriveMale();
            }
        }

        /// <summary>
        /// Suppressive homing drive male ("ffer"). Clones a WT male and replaces ONE
        /// copy of the female fertility gene (on ChromosomeListA) with the Transgene
        /// allele, giving FFER(Transgene)/FFER(WT).
        ///
        /// The Transgene locus carries:
        ///   - Cas9_male / Cas9_female (Param1): germline nuclease activity
        ///   - Cas9_maternal (Param3):           maternal deposition into the embryo
        ///   - Cas9_paternal (0):                no paternal deposition
        ///   - gRNA_FFER (1.0):                  full guide expression against FFER
        ///   - HomRepair_male / _female (Param0): HDR efficiency used when homing
        ///
        /// So the drive cuts WT FFER alleles in both germlines and homes into them;
        /// females left with two disrupted copies are sterile.
        /// </summary>
        /// <returns>A drive male, heterozygous FFER(Transgene)/FFER(WT).</returns>
        Organism Generate_HomingDriveMale()
        {
            Organism D_Male = new Organism(GenerateWTMale());

            GeneLocus FDRIVE = new GeneLocus("FFER", TargetPosition, "Transgene");
            FDRIVE.AddToTraits("Cas9_male", Simulation.Param1);
            FDRIVE.AddToTraits("Cas9_female", Simulation.Param1);
            FDRIVE.AddToTraits("Cas9_maternal", Simulation.Param3);
            FDRIVE.AddToTraits("Cas9_paternal", 0F);
            FDRIVE.AddToTraits("gRNA_FFER", 1F);
            FDRIVE.AddToTraits("HomRepair_male", Simulation.Param0);
            FDRIVE.AddToTraits("HomRepair_female", Simulation.Param0);

            // Replace the WT FFER allele on ChromosomeListA with the Transgene
            D_Male.ModifyAllele("A", FDRIVE, "WT");
            return D_Male;
        }

        /// <summary>
        /// Driving Y male ("ydrive"). Clones a WT male and converts the YLD locus on
        /// the Y chromosome to the Transgene allele, which carries the "X_shred" trait.
        /// Chromosome.Chromosome(HomChrom1, HomChrom2, parent) reads that trait during
        /// meiosis and biases sex chromosome segregation towards the Y.
        ///
        /// The locus is edited on the Y specifically rather than through ModifyAllele,
        /// because the construct must never be placed on an X: a distorter on an X
        /// would shred the chromosome carrying it.
        /// </summary>
        /// <returns>A drive male carrying one shredder Y and one wild-type X.</returns>
        Organism Generate_YDriveMale()
        {
            Organism D_Male = new Organism(GenerateWTMale());

            GeneLocus YDRIVE = new GeneLocus("YLD", DistorterPosition, "Transgene");
            YDRIVE.AddToTraits("X_shred", Simulation.XShredRate);

            foreach (Chromosome Chrom in D_Male.ChromosomeListA)
            {
                if (Chrom.ChromosomeName != "Y")
                    continue;

                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene("YLD"))
                        GL.InheritAll(YDRIVE);
                }
            }

            return D_Male;
        }

        /// <summary>
        /// MEDEA male ("medea"). Clones a WT male and converts both halves of the
        /// element — the maternally expressed toxin (MTOX) and the zygotically
        /// expressed rescue (MRES) — to Transgene on ChromosomeListA only, so the
        /// released male is heterozygous.
        ///
        /// Putting both halves on the same homolog is what makes the linkage question
        /// meaningful: a single crossover between MTOX and MRES in a heterozygote
        /// separates them, yielding a toxin-only chromosome (whose carriers make toxin
        /// but have no immunity) and a rescue-only chromosome (immune, and free of the
        /// cost of making toxin). The closer the two loci, the rarer that event.
        ///
        /// Males are released rather than females because the toxin is a maternal
        /// effect: the element must first pass through a female before it starts
        /// removing non-carriers, which costs one generation of lag.
        /// </summary>
        /// <returns>A MEDEA male, heterozygous for an intact toxin/rescue element.</returns>
        Organism Generate_MedeaMale()
        {
            Organism M_Male = new Organism(GenerateWTMale());

            GeneLocus TOXIN = new GeneLocus("MTOX", TargetPosition, "Transgene");
            TOXIN.AddToTraits("Medea_toxin", Simulation.MedeaPenetrance);

            GeneLocus RESCUE = new GeneLocus("MRES", TargetPosition + Simulation.MedeaDistance, "Transgene");
            RESCUE.AddToTraits("Medea_rescue", Simulation.MedeaRescue);

            M_Male.ModifyAllele("A", TOXIN, "WT");
            M_Male.ModifyAllele("A", RESCUE, "WT");

            return M_Male;
        }


        //----------------------- Population methods ----------------------------------------------------


        /// <summary>
        /// Performs a single mating cross between a male and female organism, producing
        /// a list of surviving offspring (eggs). The number of eggs laid is the base
        /// fecundity (GlobalEggsPerFemale) multiplied by both parents' fertility values.
        /// Each egg is created via the sexual reproduction constructor Organism(Dad, Mum),
        /// which performs meiosis, gene drive, and parental factor determination.
        ///
        /// Under the "medea" model a further, post-zygotic filter applies. A mother
        /// carrying the toxin half of the element loads every egg she lays; each embryo
        /// is then tested individually and those that fail to inherit a working rescue
        /// arrest and are not returned. The eggs reported to the output are therefore
        /// the viable ones, so the embryonic load the element imposes is visible as a
        /// dip in egg number while it spreads.
        /// </summary>
        /// <param name="Dad">The paternal organism (must be male).</param>
        /// <param name="Mum">The maternal organism (must be female).</param>
        /// <param name="GlobalEggsPerFemale">Base number of eggs per cross.</param>
        /// <returns>List of viable offspring organisms.</returns>
        public List<Organism> PerformCross(Organism Dad, Organism Mum, int GlobalEggsPerFemale)
        {
            int EggsPerFemale = GlobalEggsPerFemale;
            List<Organism> EggList = new List<Organism>();

            // Adjust egg count by parental fertility
            EggsPerFemale = (int)(EggsPerFemale * Dad.GetFertility() * Mum.GetFertility());

            // A MEDEA mother deposits toxin into every egg regardless of its genotype.
            float MaternalToxin = 0F;
            if (Simulation.Model == "medea")
                MaternalToxin = Mum.GetMaxTransgeneTrait("Medea_toxin");

            for (int i = 0; i < EggsPerFemale; i++)
            {
                Organism Egg = new Organism(Dad, Mum);

                if (MaternalToxin > 0F && !Egg.SurvivesMedeaToxin(MaternalToxin))
                    continue;   // embryonic arrest: the egg is laid but never hatches

                EggList.Add(Egg);
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
        ///   1. With 50% probability, swap ChromosomeListA and ListB
        ///      (randomizes which parental chromosome set is in which list).
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
