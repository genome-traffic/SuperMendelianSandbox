using System;
using System.Collections.Generic;
using System.Linq;

namespace SMS
{
    /// <summary>
    /// Represents a single chromosome carrying an ordered list of gene loci.
    /// Chromosomes belong to named homologous pairs (autosomal: "1", "2", "3";
    /// sex: "Sex") and have individual names ("1", "2", "3", "X", "Y").
    ///
    /// Key responsibilities:
    ///   - Deep cloning of chromosomes for independent genetic lineages.
    ///   - Meiotic recombination (crossover between homologous chromosomes).
    ///   - CRISPR/Cas9 gene drive mechanics: cutting WT alleles and either
    ///     homing (HDR → Transgene copy) or generating resistance alleles (NHEJ → R1/R2).
    ///
    /// Four constructors cover distinct biological scenarios:
    ///   1. Empty chromosome (new locus list)
    ///   2. Clone (deep copy)
    ///   3. Meiosis with gene drive (CRISPR cutting + recombination)
    ///   4. Simple recombination (crossover only, no gene drive)
    /// </summary>
    class Chromosome
    {
        string chromosomename;
        string homologouspairname;
        string[] possiblechromnames = { "1", "2", "3", "X", "Y" };
        string[] possiblechrompairnames = { "1", "2", "3", "Sex" };

        /// <summary>
        /// Ordered list of gene loci on this chromosome. Loci are assumed to be
        /// in the same order on both homologous chromosomes so that index-based
        /// pairing is valid during recombination and gene drive operations.
        /// </summary>
        public List<GeneLocus> GeneLocusList
        {get;set;}

        /// <summary>
        /// Identifies which homologous pair this chromosome belongs to.
        /// Autosomal pairs: "1", "2", "3". Sex chromosome pair: "Sex".
        /// Validated against possiblechrompairnames on assignment.
        /// </summary>
        public string HomologousPairName
        {
            get { return homologouspairname; }
            set
            {
                if (possiblechrompairnames.Contains(value))
                    homologouspairname = value;
                else
                    throw new ArgumentException("not a pair name");
            }
        }

        /// <summary>
        /// The specific chromosome identity within a pair.
        /// Autosomal: "1", "2", "3". Sex: "X" or "Y".
        /// Validated against possiblechromnames on assignment.
        /// </summary>
        public string ChromosomeName
        {
            get { return chromosomename; }
            set
            {
                if (possiblechromnames.Contains(value))
                    chromosomename = value;
                else
                    throw new ArgumentException("not a chrom name");
            }
        }

        /// <summary>
        /// Creates a new empty chromosome with no gene loci.
        /// Used as a starting point when manually building chromosome content
        /// (e.g., in organism generator methods).
        /// </summary>
        /// <param name="CName">Chromosome name ("1","2","3","X","Y").</param>
        /// <param name="PName">Homologous pair name ("1","2","3","Sex").</param>
        public Chromosome(string CName, string PName)
        {
            this.ChromosomeName = CName;
            this.HomologousPairName = PName;
            this.GeneLocusList = new List<GeneLocus>();
        }

        /// <summary>
        /// Deep-copy constructor. Creates an independent clone of an existing chromosome,
        /// including deep copies of all gene loci. Used during gamete formation and
        /// organism cloning to ensure mutations/modifications to the copy do not
        /// affect the original.
        /// </summary>
        /// <param name="Old">The chromosome to clone.</param>
        public Chromosome(Chromosome Old)
        {
            this.ChromosomeName = Old.ChromosomeName;
            this.HomologousPairName = Old.HomologousPairName;
            this.GeneLocusList = new List<GeneLocus>();

            foreach (GeneLocus OldGL in Old.GeneLocusList)
            {
                GeneLocus NewGL = new GeneLocus(OldGL);
                GeneLocusList.Add(NewGL);
            }
        }

        /// <summary>
        /// Meiotic constructor with CRISPR gene drive mechanics. Produces a single
        /// recombinant gamete chromosome from two homologous parental chromosomes.
        ///
        /// For sex chromosomes ("Sex" pair): No recombination or gene drive occurs.
        /// One of the two homologs (e.g., X or Y) is chosen at random with 50/50
        /// probability, simulating Mendelian segregation of sex chromosomes.
        ///
        /// For autosomes: The process is:
        ///   1. Clone both homologs to avoid modifying the parent's genome.
        ///   2. Apply CRISPR/Cas9 gene drive on both copies: for each gRNA-target
        ///      pair defined in Simulation.Target_cognate_gRNA, attempt to cut WT
        ///      alleles on each homolog and home from the other (bidirectional).
        ///      The Cas9 activity level is sex-specific ("Cas9_male" or "Cas9_female").
        ///      HDRReduction is 0 here (full germline HDR efficiency).
        ///   3. After gene drive, produce the final gamete chromosome by simple
        ///      recombination between the two (possibly modified) homologs.
        /// </summary>
        /// <param name="HomChrom1">First homologous chromosome (from ChromosomeListA).</param>
        /// <param name="HomChrom2">Second homologous chromosome (from ChromosomeListB).</param>
        /// <param name="parent">The parent organism, used to query Cas9/gRNA levels and sex.</param>
        public Chromosome(Chromosome HomChrom1, Chromosome HomChrom2, Organism parent)
        {
            this.GeneLocusList = new List<GeneLocus>();

            if (HomChrom1.HomologousPairName != HomChrom2.HomologousPairName)
            { throw new System.ArgumentException("Not homologous Chromosomes", "warning"); }

            // Sex chromosomes: no recombination, randomly pick one homolog (X or Y)
            if (HomChrom1.HomologousPairName == "Sex")
            {
                if (Shuffle.random.Next(0, 2) != 0)
                {
                    this.ChromosomeName = HomChrom1.ChromosomeName;
                    this.HomologousPairName = HomChrom1.HomologousPairName;

                    foreach (GeneLocus OldGL in HomChrom1.GeneLocusList)
                    {
                        GeneLocus NewGL = new GeneLocus(OldGL);
                        GeneLocusList.Add(NewGL);
                    }
                }
                else
                {
                    this.ChromosomeName = HomChrom2.ChromosomeName;
                    this.HomologousPairName = HomChrom2.HomologousPairName;

                    foreach (GeneLocus OldGL in HomChrom2.GeneLocusList)
                    {
                        GeneLocus NewGL = new GeneLocus(OldGL);
                        GeneLocusList.Add(NewGL);
                    }

                }
            }
            else
            {
                // Autosomes: apply gene drive then recombine
                this.ChromosomeName = HomChrom1.ChromosomeName;
                this.HomologousPairName = HomChrom1.HomologousPairName;

                // Clone both homologs so gene drive modifications don't affect the parent
                Chromosome HC1 = new Chromosome(HomChrom1);
                Chromosome HC2 = new Chromosome(HomChrom2);

                #region Cas9 activity / homing at all loci
                // Determine the parent's sex-specific Cas9 level from all Transgene loci
                float Cas9level = parent.GetTransgeneLevel("Cas9_" + parent.GetSex());

                if (Cas9level > 0)
                {
                    // For each gRNA-target pair (e.g., FFER↔gRNA_FFER, TRA↔gRNA_TRA),
                    // attempt bidirectional homing: each homolog tries to convert WT
                    // alleles on the OTHER homolog using its Transgene as template
                    for (int u = 0; u < Simulation.Target_cognate_gRNA.GetLength(0); u++)
                    {
                        float gRNAlevel = parent.GetTransgeneLevel(Simulation.Target_cognate_gRNA[u, 1]);
                        string gRNAtarget = Simulation.Target_cognate_gRNA[u, 0];

                        HC1.CutAndHomeInto(HC2, parent.GetSex(), Cas9level, gRNAlevel, gRNAtarget, 0F);
                        HC2.CutAndHomeInto(HC1, parent.GetSex(), Cas9level, gRNAlevel, gRNAtarget, 0F);
                    }
                }
                #endregion

                // Produce the final gamete by simple crossover recombination between
                // the two (now possibly gene-drive-modified) homologs
                this.GeneLocusList = new Chromosome(HC1, HC2).GeneLocusList;

            }


        }

        /// <summary>
        /// Simple recombination constructor. Produces a recombinant chromosome from two
        /// homologous chromosomes using crossover based on inter-locus recombination
        /// frequencies. No gene drive mechanics are applied.
        ///
        /// Algorithm:
        ///   - For the first locus, randomly choose from HomChrom1 or HomChrom2 (50/50).
        ///   - For each subsequent locus, the probability of a crossover (switching to the
        ///     other homolog) is determined by RecFreq between adjacent loci. If the random
        ///     draw exceeds the recombination frequency, stay on the current homolog;
        ///     otherwise, cross over to the other.
        ///   - This models linked inheritance: loci close together tend to stay together,
        ///     while distant loci assort more independently.
        /// </summary>
        /// <param name="HomChrom1">First homologous chromosome.</param>
        /// <param name="HomChrom2">Second homologous chromosome.</param>
        public Chromosome(Chromosome HomChrom1, Chromosome HomChrom2)
        {
            this.ChromosomeName = HomChrom1.ChromosomeName;
            this.HomologousPairName = HomChrom1.HomologousPairName;
            this.GeneLocusList = new List<GeneLocus>();

            bool listone = true;  // Tracks which homolog we're currently copying from
            for (var i = 0; i < HomChrom1.GeneLocusList.Count; i++)
            {

                if (i == 0)
                {
                    // First locus: random 50/50 choice of which homolog to start from
                    if (Shuffle.random.Next(0, 2) != 0)
                    {
                        this.GeneLocusList.Add(new GeneLocus(HomChrom1.GeneLocusList[i]));
                        listone = true;
                    }
                    else
                    {
                        this.GeneLocusList.Add(new GeneLocus(HomChrom2.GeneLocusList[i]));
                        listone = false;
                    }
                }
                else
                {
                    // Subsequent loci: check for crossover based on recombination frequency
                    // between this locus and the previous one
                    if (listone == true)
                    {
                        // Currently on HomChrom1; crossover if RecFreq >= random draw
                        if (HomChrom1.GeneLocusList[i].RecFreq(HomChrom1.GeneLocusList[i - 1]) < (float)Shuffle.random.NextDouble())
                        {
                            this.GeneLocusList.Add(new GeneLocus(HomChrom1.GeneLocusList[i]));
                        }
                        else
                        {
                            this.GeneLocusList.Add(new GeneLocus(HomChrom2.GeneLocusList[i]));
                            listone = false;
                        }
                    }
                    else
                    {
                        // Currently on HomChrom2; crossover if RecFreq >= random draw
                        if (HomChrom2.GeneLocusList[i].RecFreq(HomChrom2.GeneLocusList[i - 1]) < (float)Shuffle.random.NextDouble())
                        {
                            this.GeneLocusList.Add(new GeneLocus(HomChrom2.GeneLocusList[i]));
                        }
                        else
                        {
                            this.GeneLocusList.Add(new GeneLocus(HomChrom1.GeneLocusList[i]));
                            listone = true;
                        }
                    }
                }



            }
        }

        /// <summary>
        /// Returns true if this chromosome belongs to the sex chromosome pair ("Sex").
        /// Used to skip gene drive activity on sex chromosomes (gene drive only targets
        /// autosomal loci in the zygotic context) and to handle sex chromosome segregation
        /// differently during meiosis.
        /// </summary>
        public bool IsSexChrom()
        {
            if (this.homologouspairname == "Sex")
                return true;
            else
                return false;
        }

        /// <summary>
        /// Simulates CRISPR/Cas9 cutting and gene drive homing on this chromosome,
        /// using a source (template) chromosome for HDR.
        ///
        /// For each locus on THIS chromosome:
        ///   1. Verify the locus is the same gene as the corresponding locus on SourceChrom.
        ///   2. Check if the locus matches the gRNA target gene.
        ///   3. Only WT alleles can be cut (Transgene, R1, R2 are resistant to cutting).
        ///   4. Cutting occurs stochastically: both Cas9level and gRNAlevel must each
        ///      independently pass a random probability check.
        ///   5. If cut occurs, determine repair outcome:
        ///      a. Homology-directed repair (HDR): with probability = HomRepair trait of the
        ///         source Transgene allele (sex-specific), the WT locus is converted to a
        ///         full copy of the source Transgene (InheritAll). This is the "homing" step.
        ///      b. Non-homologous end joining (NHEJ): if HDR fails, the cut is repaired
        ///         imprecisely. The Conservation trait determines the outcome:
        ///         - High conservation → R2 (loss-of-function resistance allele)
        ///         - Low conservation → R1 (functional resistance allele)
        ///
        /// The HDRReduction parameter reduces HDR efficiency: the fetched HomRepair rate
        /// is multiplied by (1 - HDRReduction). In germline calls (HDRReduction=0), the full
        /// HDR rate applies. In zygotic calls (HDRReduction=0.99), HDR is reduced by 99%.
        /// </summary>
        /// <param name="SourceChrom">The homologous chromosome providing the Transgene template for HDR.</param>
        /// <param name="sex">Sex of the organism ("male" or "female"), used to select sex-specific HDR rate.</param>
        /// <param name="Cas9level">Probability of Cas9 cutting (0–1).</param>
        /// <param name="gRNAlevel">Probability that the gRNA guides Cas9 to the target (0–1).</param>
        /// <param name="gRNAtarget">The gene name that this gRNA targets (e.g., "TRA", "FFER").</param>
        /// <param name="HDRReduction">Factor reducing HDR efficiency (0 = no reduction, 1 = complete block).
        /// Applied as: effective_HDR = HomRepair * (1 - HDRReduction).</param>
        public void CutAndHomeInto(Chromosome SourceChrom, string sex, float Cas9level, float gRNAlevel,string gRNAtarget, float HDRReduction)
        {

            for (var i = 0; i < this.GeneLocusList.Count; i++)
            {
                // Verify loci correspond to the same gene on both chromosomes
                if (this.GeneLocusList[i].IsSameGene(SourceChrom.GeneLocusList[i]))
                {
                    // Check if this locus is targeted by the current gRNA
                    if (this.GeneLocusList[i].IsSameGene(gRNAtarget))
                    {
                        // Only WT alleles can be cut; Transgene/R1/R2 are resistant
                        if (this.GeneLocusList[i].IsSameAllele("WT"))
                        {
                            // Stochastic cutting: both Cas9 and gRNA must succeed independently
                            if (Cas9level >= (float)Shuffle.random.NextDouble() && gRNAlevel >= (float)Shuffle.random.NextDouble())
                            {
                                // Get the source Transgene's HDR rate for this sex
                                float HomRepair = SourceChrom.GeneLocusList[i].GetOutTraitValue("HomRepair_" + sex);
                                // Apply HDR reduction (0 in germline = no effect; 0.99 in zygotic = 99% reduction)
                                HomRepair = HomRepair * (1 - HDRReduction);
                                float Cons = 0;
                                // Get the conservation level of the WT target site
                                Cons = this.GeneLocusList[i].GetOutTraitValue("Conservation");

                                if (HomRepair >= (float)Shuffle.random.NextDouble())
                                {
                                    // HDR success: copy the entire Transgene locus onto this WT locus
                                    this.GeneLocusList[i].InheritAll(SourceChrom.GeneLocusList[i]);
                                }
                                else
                                {
                                    // NHEJ: imprecise repair creates a resistance allele
                                    if (Cons >= (float)Shuffle.random.NextDouble())
                                        this.GeneLocusList[i].AlleleName = "R2";  // Conserved site → loss-of-function
                                    else
                                        this.GeneLocusList[i].AlleleName = "R1";  // Non-conserved → functional resistance
                                }
                            }
                        }
                    }
                }
            }

        }

    }
}
