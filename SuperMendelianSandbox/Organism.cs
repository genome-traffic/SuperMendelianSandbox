using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace SMS
{
    /// <summary>
    /// Represents a single diploid organism in the agent-based gene drive simulation.
    /// Each organism carries two parallel lists of chromosomes (ChromosomeListA and
    /// ChromosomeListB) representing the two homologous sets inherited from each parent.
    /// Chromosomes at the same index in each list are homologous pairs.
    ///
    /// The organism also carries a ParentalFactors dictionary representing non-genomic
    /// (cytoplasmic/parental) contributions inherited from the parents at conception,
    /// including:
    ///   - "Cas9"      — Parentally deposited Cas9 protein/mRNA in the zygote
    ///   - "gRNA_TRA"  — Parentally deposited gRNA targeting TRA
    ///   - "gRNA_FFER" — Parentally deposited gRNA targeting FFER
    ///   - "TRA_mRNA"  — Maternal TRA mRNA provision (determines female differentiation)
    ///
    /// Sex determination follows a two-component system:
    ///   1. MoY (Maleness-on-Y): A male-determining factor on the Y chromosome.
    ///      Presence of a WT MoY allele initiates male development.
    ///   2. TRA (Transformer): Maternal TRA mRNA must be present AND the organism must
    ///      carry at least one functional TRA allele (WT or R1) for female development.
    ///      Without maternal TRA mRNA or without a functional TRA allele, the organism
    ///      develops as male regardless of karyotype. This is the gene drive target:
    ///      disrupting TRA converts XX individuals into phenotypic males (sterile
    ///      intersexes), collapsing the female population.
    /// </summary>
    class Organism
    {
        /// <summary>
        /// First set of chromosomes (one from each homologous pair). In offspring,
        /// this receives the father's gamete chromosomes.
        /// </summary>
        public List<Chromosome> ChromosomeListA
        {get;set;}

        /// <summary>
        /// Second set of chromosomes (one from each homologous pair). In offspring,
        /// this receives the mother's gamete chromosomes.
        /// </summary>
        public List<Chromosome> ChromosomeListB
        {get;set;}

        /// <summary>
        /// Non-genomic factors inherited from the parents at conception. These represent
        /// maternally/paternally deposited proteins and mRNAs that act in the zygote
        /// before the organism's own genome is expressed. Key entries:
        ///   "Cas9"      — Cas9 protein deposited by parents (drives zygotic gene conversion)
        ///   "gRNA_TRA"  — gRNA for TRA deposited by parents
        ///   "gRNA_FFER" — gRNA for FFER deposited by parents
        ///   "TRA_mRNA"  — Maternal TRA mRNA (1.0 = present, 0.0 = absent)
        /// </summary>
        Dictionary<string, float> ParentalFactors;

        /// <summary>
        /// Default constructor: creates an empty organism with no chromosomes and no
        /// parental factors. Used as a blank slate by the organism generator methods
        /// (GenerateWTFemale, GenerateWTMale, Generate_DriveMale) which then manually
        /// add chromosomes and set parental factors.
        /// </summary>
        public Organism()
        {
            this.ChromosomeListA = new List<Chromosome>();
            this.ChromosomeListB = new List<Chromosome>();
            this.ParentalFactors = new Dictionary<string, float>();
        }

        /// <summary>
        /// Clone constructor: creates a deep copy of an existing organism. All chromosomes
        /// and their gene loci are independently cloned, as are the parental factors.
        /// Used when organisms are transferred between populations (migration, release
        /// events) or when promoting eggs to adults, ensuring each copy is genetically
        /// independent.
        /// </summary>
        /// <param name="Old">The organism to clone.</param>
        public Organism(Organism Old)
        {
            this.ChromosomeListA = new List<Chromosome>();
            this.ChromosomeListB = new List<Chromosome>();

            Old.ChromosomeListA.ForEach((item) =>
            {
                this.ChromosomeListA.Add(new Chromosome(item));
            });

            Old.ChromosomeListB.ForEach((item) =>
            {
                this.ChromosomeListB.Add(new Chromosome(item));
            });

            this.ParentalFactors = new Dictionary<string, float>();

            foreach (var OldFactor in Old.ParentalFactors)
            {
                this.ParentalFactors.Add(OldFactor.Key, OldFactor.Value);
            }

        }

        /// <summary>
        /// Sexual reproduction constructor: creates a new offspring from two parents via
        /// meiosis and fertilization. This is the core reproductive event in the simulation.
        ///
        /// Steps:
        ///   1. Generate gametes from each parent via GetGametChromosomeList(), which
        ///      performs meiotic recombination and germline gene drive for each chromosome.
        ///      Dad's gamete → ChromosomeListA, Mum's gamete → ChromosomeListB.
        ///   2. Determine parental Cas9/gRNA deposition into the zygote:
        ///      - Cas9 comes from maternal "Cas9_maternal" + paternal "Cas9_paternal" traits.
        ///      - gRNA for each target comes from both parents (summed, clamped to [0,1]).
        ///      These deposited factors enable zygotic gene drive activity (post-fertilization
        ///      cutting in the embryo).
        ///   3. Determine maternal TRA mRNA provision:
        ///      - If Mum carries an R1 allele of TRA: TRA_mRNA = 1 (R1 is functional, produces TRA).
        ///      - If Mum has NO WT TRA alleles: TRA_mRNA = 0 (no functional TRA to transcribe).
        ///      - If Mum has WT TRA but also carries the gene drive (Cas9+gRNA_TRA):
        ///        stochastically determine if the drive destroys TRA mRNA in the mother's
        ///        germline. If Cas9 and gRNA both fire → TRA_mRNA = 0; otherwise → 1.
        ///      Without maternal TRA mRNA, offspring develop as male regardless of their own
        ///      TRA genotype (key mechanism of the sex-distortion gene drive).
        /// </summary>
        /// <param name="Dad">The paternal organism.</param>
        /// <param name="Mum">The maternal organism.</param>
        public Organism(Organism Dad, Organism Mum)
        {
            this.ChromosomeListA = new List<Chromosome>();
            this.ChromosomeListB = new List<Chromosome>();

            // Meiosis: each parent produces a haploid gamete chromosome set
            this.ChromosomeListA.AddRange(Dad.GetGametChromosomeList());
            this.ChromosomeListB.AddRange(Mum.GetGametChromosomeList());

            this.ParentalFactors = new Dictionary<string, float>();

            //determine parental factors

            #region Cas9/gRNA deposition
            // Calculate combined parental Cas9 deposition into the zygote.
            // Maternal Cas9_maternal + Paternal Cas9_paternal, clamped to [0,1].
            float Cas9deposit = 0;
            Cas9deposit = Mum.GetTransgeneLevel("Cas9_maternal") + Dad.GetTransgeneLevel("Cas9_paternal");


            if (Cas9deposit > 1)
                Cas9deposit = 1;
            else if (Cas9deposit < 0)
                Cas9deposit = 0;

            this.ParentalFactors.Add("Cas9", Cas9deposit);

            // For each gRNA target, calculate combined parental gRNA deposition.
            // Both parents can contribute gRNA (summed, clamped to [0,1]).
            for (int u = 0; u < Simulation.Target_cognate_gRNA.GetLength(0); u++)
            {
                float gRNAdeposit = 0;
                gRNAdeposit = Mum.GetTransgeneLevel(Simulation.Target_cognate_gRNA[u, 1]) + Dad.GetTransgeneLevel(Simulation.Target_cognate_gRNA[u, 1]);

                if (gRNAdeposit > 1)
                    gRNAdeposit = 1;
                else if (gRNAdeposit < 0)
                    gRNAdeposit = 0;

                this.ParentalFactors.Add(Simulation.Target_cognate_gRNA[u, 1], gRNAdeposit);
            }
            #endregion

            #region determine maternal TRA provision
            // Maternal TRA mRNA provision determines whether the offspring can develop
            // as female. This models the biological requirement for maternally-loaded
            // TRA transcript to initiate the female sex determination cascade.

            this.ParentalFactors["TRA_mRNA"] = 0F;

            if (Mum.AllelePresent("TRA", "R1"))
            {
                // R1 is a functional resistance allele — it still produces TRA mRNA
                this.ParentalFactors["TRA_mRNA"] = 1F;
            }
            else
            {
                if (!(Mum.AllelePresent("TRA", "WT")))
                {
                    // Mother has no functional TRA allele (Transgene/R2 only) → no TRA mRNA
                    this.ParentalFactors["TRA_mRNA"] = 0F;
                }
                else
                {
                    // Mother has at least one WT TRA allele, but may also carry the drive.
                    // Stochastically check if Cas9+gRNA destroys the WT TRA mRNA in the
                    // mother's germline before it can be deposited into the egg.
                    float Cas9level = Mum.GetTransgeneLevel("Cas9_female");
                    float tragRNAlevel = Mum.GetTransgeneLevel("gRNA_TRA");

                    if (Cas9level >= (float)Shuffle.random.NextDouble() && tragRNAlevel >= (float)Shuffle.random.NextDouble())
                    { this.ParentalFactors["TRA_mRNA"] = 0F; }  // Drive destroys maternal TRA mRNA
                    else
                    { this.ParentalFactors["TRA_mRNA"] = 1F; }  // TRA mRNA deposited successfully

                }
            }

            #endregion


        }

        #region Organism methods

        /// <summary>
        /// Adds or updates a parental factor (non-genomic, cytoplasmically inherited
        /// trait) for this organism. Used when constructing template organisms to set
        /// initial values (e.g., TRA_mRNA = 1 for wild-type females).
        /// </summary>
        /// <param name="name">Factor name (e.g., "TRA_mRNA", "Cas9").</param>
        /// <param name="value">Factor value (typically 0 or 1).</param>
        public void AddToParentalFactors(string name, float value)
        {
            this.ParentalFactors[name] = value;
        }

        /// <summary>
        /// Produces a haploid gamete chromosome set via meiosis. For each homologous
        /// pair (indexed in parallel across ChromosomeListA and ChromosomeListB),
        /// creates a single recombinant chromosome using the meiotic constructor
        /// (Chromosome(HomChrom1, HomChrom2, parent)) which applies CRISPR gene drive
        /// mechanics followed by crossover recombination.
        ///
        /// This is called once per parent during sexual reproduction to generate the
        /// chromosomes that will be passed to the offspring.
        /// </summary>
        /// <returns>A list of haploid chromosomes (one per homologous pair).</returns>
        public List<Chromosome> GetGametChromosomeList()
        {
            List<Chromosome> GametChroms = new List<Chromosome>();

            for (int i = 0; i < this.ChromosomeListA.Count; i++)
            {
                GametChroms.Add(new Chromosome(this.ChromosomeListA[i], this.ChromosomeListB[i], this));
            }
            return GametChroms;
        }

        /// <summary>
        /// Replaces a specific allele at a specific gene locus on one chromosome set
        /// (A or B) with data from a new GeneLocus. Searches all chromosomes in the
        /// specified set for loci matching NewLocus's gene name AND carrying the
        /// specified allele, then overwrites them via InheritAll.
        ///
        /// Used to inject transgene constructs into template organisms (e.g., replacing
        /// a WT TRA allele with a Transgene TRA allele on ChromosomeListA of a drive male).
        /// </summary>
        /// <param name="AorB">"A" to modify ChromosomeListA, "B" for ChromosomeListB.</param>
        /// <param name="NewLocus">The replacement locus (carries new allele name and traits).</param>
        /// <param name="AlleleToReplace">The allele name to search for and replace (e.g., "WT").</param>
        public void ModifyAllele(string AorB, GeneLocus NewLocus, string AlleleToReplace)
        {
            if (AorB == "A")
                foreach (Chromosome Chrom in this.ChromosomeListA)
                {
                    foreach (GeneLocus GL in Chrom.GeneLocusList)
                    {
                        if (GL.IsSameGene(NewLocus))
                        {
                            if (GL.IsSameAllele(AlleleToReplace))
                            {
                                GL.InheritAll(NewLocus);
                            }
                        }
                    }
                }
            else if (AorB == "B")
                foreach (Chromosome Chrom in this.ChromosomeListB)
                {
                    foreach (GeneLocus GL in Chrom.GeneLocusList)
                    {
                        if (GL.IsSameGene(NewLocus))
                        {
                            if (GL.IsSameAllele(AlleleToReplace))
                            {
                                GL.InheritAll(NewLocus);
                            }
                        }
                    }
                }
            else
                throw new ArgumentException("neither A nor B name");


        }

        /// <summary>
        /// Returns the sex chromosome karyotype as a string (e.g., "XX", "XY", "YX").
        /// Concatenates the ChromosomeName of the sex chromosome from ListA and ListB.
        /// Used for output/tracking to distinguish genetic sex (karyotype) from
        /// phenotypic sex (which depends on TRA and MoY).
        /// </summary>
        /// <returns>Karyotype string, e.g., "XX" or "XY".</returns>
        public string GetSexChromKaryo()
        {
            string karyo = "";

            //role of sex chromosomes
            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                if (Chrom.IsSexChrom())
                {
                    karyo = Chrom.ChromosomeName;
                    break;
                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                if (Chrom.IsSexChrom())
                {
                    karyo += Chrom.ChromosomeName;
                    break;
                }
            }

            return karyo;

        }

        /// <summary>
        /// Determines the phenotypic sex of the organism using a two-component system:
        ///
        /// 1. MoY (Maleness-on-Y) check: If a WT MoY allele is present on any chromosome
        ///    (normally the Y), the baseline sex is set to "male". Otherwise "female".
        ///
        /// 2. TRA (Transformer) override: Regardless of MoY status:
        ///    a. If maternal TRA_mRNA was NOT deposited (< 1.0), the organism is male.
        ///       This is how the gene drive causes sex conversion in XX individuals.
        ///    b. If the organism carries a functional TRA allele (WT or R1), the MoY-based
        ///       sex determination stands (XX→female, XY→male).
        ///    c. If NO functional TRA allele is present (only Transgene and/or R2),
        ///       the organism defaults to male (sex conversion).
        ///
        /// The net effect: XX organisms without functional TRA (either lacking maternal
        /// TRA mRNA or lacking a WT/R1 TRA allele) become phenotypic males, which is the
        /// population-suppression mechanism of this gene drive system.
        /// </summary>
        /// <returns>"male" or "female".</returns>
        public string GetSex()
        {
            string sex = "female";

            // Step 1: Check for MoY male-determining factor (normally on Y chromosome)
            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene("MoY") && GL.IsSameAllele("WT"))
                    sex = "male";
                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene("MoY") && GL.IsSameAllele("WT"))
                    sex = "male";
                }
            }

            // Step 2: TRA override — without maternal TRA mRNA, organism is always male
            if (this.ParentalFactors["TRA_mRNA"] < 1F)
            { return "male"; }

            // Step 3: If organism has at least one functional TRA allele (WT or R1),
            // use the MoY-based sex. Otherwise default to male (sex conversion).
            if (this.AllelePresent("TRA", "WT") || this.AllelePresent("TRA", "R1"))
            { return sex; }
            else
            { return "male"; }

        }

        /// <summary>
        /// Convenience method: returns true if phenotypic sex is male.
        /// </summary>
        public bool IsMale()
        {
            if (this.GetSex() == "male")
                return true;
            else
                return false;
        }

        /// <summary>
        /// Convenience method: returns true if phenotypic sex is female.
        /// </summary>
        public bool IsFemale()
        {
            if (this.GetSex() == "male")
                return false;
            else
                return true;
        }

        /// <summary>
        /// Returns the diploid genotype for a given gene as a comma-separated string
        /// of the two allele names, sorted alphabetically (e.g., "Transgene,WT" or
        /// "R1,WT"). Used for genotype frequency tracking in the simulation output.
        /// The alphabetical sorting ensures consistent genotype naming regardless of
        /// which chromosome list carries which allele.
        /// </summary>
        /// <param name="WhichGene">The gene name to query (e.g., "TRA", "FFER").</param>
        /// <returns>Sorted genotype string like "WT,WT" or "Transgene,WT".</returns>
        public string GetGenotype(string WhichGene)
        {
            string output = "error";
            string GT1 = "";
            string GT2 = "";

            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichGene))
                    {
                        GT1 = GL.AlleleName;
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichGene))
                    {
                        GT2 = GL.AlleleName;
                    }

                }
            }

            int c = string.Compare(GT1, GT2);
            if (c >= 0)
                return GT1 + "," + GT2;
            else if (c == -1)
                return GT2 + "," + GT1;
            else
                return output;
        }

        /// <summary>
        /// Calculates the fertility of this organism as a multiplier on egg production
        /// (0.0 = sterile, 1.0 = fully fertile). Currently returns 1.0 for all organisms
        /// since the genotype-specific fertility penalties are commented out. These
        /// commented-out blocks show a previous or alternative gene drive design targeting
        /// the ZPG (zero population growth) gene, where certain transgene/R2 genotypes
        /// caused partial or complete sterility. Additional commented blocks show
        /// fertility penalties for R2 homozygosity at Aper1, AP2, and CP loci.
        ///
        /// The result is clamped to [0, 1].
        /// </summary>
        /// <returns>Fertility multiplier in [0, 1].</returns>
        public float GetFertility()
        {
            float fer = 1.0F;

            if (this.GetSex() == "male")
            {
                //if (this.AlleleHomozygous("ZPG", "Transgene"))
                //{ fer -= 0.59F; }
                //else if (this.AlleleHeterozygous("ZPG", "Transgene", "ZPG", "R2"))
                //{ fer = 0F; }
                //else if (this.AlleleHomozygous("ZPG", "R2"))
                //{ fer = 0F; }

            }
            else
            {
                //if (this.AlleleHomozygous("ZPG", "Transgene"))
                //{ fer = 0F; }
                //else if (this.AlleleHeterozygous("ZPG", "Transgene", "ZPG", "R2"))
                //{ fer = 0F; }
                //else if (this.AlleleHomozygous("ZPG", "R2"))
                //{ fer = 0F; }
                //else if (this.AlleleHeterozygous("ZPG", "Transgene", "ZPG", "WT"))
                //{ fer = 0F; }
            }

            //if (this.AlleleHomozygous("Aper1", "R2"))
            //{ fer -= 0.25F; }

            //if (this.AlleleHomozygous("AP2", "R2"))
            //{ fer -= 0.25F; }

            //if (this.AlleleHomozygous("CP", "R2"))
            //{ fer -= 0.25F; }


            if (fer < 0F)
            { fer = 0F; }
            else if (fer > 1F)
            { fer = 1F; }

            return fer;

        }

        /// <summary>
        /// Checks whether at least one copy of a specified allele is present at the
        /// given gene across both chromosome sets. Returns true if found on either
        /// ChromosomeListA or ChromosomeListB. Used for genotype queries in sex
        /// determination and fertility calculations.
        /// </summary>
        /// <param name="WhichGene">Gene name to search for.</param>
        /// <param name="WhichAllele">Allele name to match (e.g., "WT", "Transgene").</param>
        /// <returns>True if the allele is present on at least one homolog.</returns>
        public bool AllelePresent(string WhichGene, string WhichAllele)
        {

            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichGene))
                    {
                        if (GL.AlleleName == WhichAllele)
                        { return true; }
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichGene))
                    {
                        if (GL.AlleleName == WhichAllele)
                        { return true; }
                    }

                }
            }

            return false;
        }

        /// <summary>
        /// Overload of AllelePresent that takes a GeneLocus object, matching by both
        /// gene name and allele name.
        /// </summary>
        public bool AllelePresent(GeneLocus WhichLocus)
        {

            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichLocus))
                    {
                        if (GL.AlleleName == WhichLocus.AlleleName)
                        { return true; }
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichLocus))
                    {
                        if (GL.AlleleName == WhichLocus.AlleleName)
                        { return true; }
                    }

                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether the organism is homozygous for a given allele at a given gene
        /// (i.e., the same allele appears on BOTH ChromosomeListA and ChromosomeListB).
        /// First checks ListA, then confirms the match in ListB. Used for fertility
        /// penalty checks in the ZPG-based gene drive design (currently commented out).
        /// </summary>
        /// <param name="WhichGene">Gene name to check.</param>
        /// <param name="WhichAllele">Allele that must be present on both homologs.</param>
        /// <returns>True if homozygous for the specified allele.</returns>
        public bool AlleleHomozygous(string WhichGene, string WhichAllele)
        {
            bool oneisthere = false;

            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichGene))
                    {
                        if (GL.AlleleName == WhichAllele)
                        { oneisthere = true; }
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichGene))
                    {
                        if (GL.AlleleName == WhichAllele && oneisthere)
                        { return true; }
                    }

                }
            }

            return false;
        }

        /// <summary>
        /// Overload of AlleleHomozygous that takes a GeneLocus object.
        /// </summary>
        public bool AlleleHomozygous(GeneLocus WhichLocus)
        {
            bool oneisthere = false;

            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichLocus))
                    {
                        if (GL.AlleleName == WhichLocus.AlleleName)
                        { oneisthere = true; }
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichLocus))
                    {
                        if (GL.AlleleName == WhichLocus.AlleleName && oneisthere)
                        { return true; }
                    }

                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether the organism is heterozygous for two specific alleles at
        /// (potentially different) genes. Returns true if Allele1 at Gene1 AND Allele2
        /// at Gene2 are both present somewhere in the organism's genome. Note: this
        /// checks across BOTH chromosome lists for each allele independently, so it
        /// doesn't strictly verify they are on opposite homologs — it confirms both
        /// alleles exist somewhere in the diploid genome. Used for compound genotype
        /// checks in the ZPG fertility system (currently commented out).
        /// </summary>
        /// <param name="WhichGene1">First gene name.</param>
        /// <param name="WhichAllele1">First allele to find.</param>
        /// <param name="WhichGene2">Second gene name.</param>
        /// <param name="WhichAllele2">Second allele to find.</param>
        /// <returns>True if both alleles are present.</returns>
        public bool AlleleHeterozygous(string WhichGene1, string WhichAllele1, string WhichGene2, string WhichAllele2)
        {
            bool oneisthere = false;
            bool twoisthere = false;

            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichGene1))
                    {
                        if (GL.AlleleName == WhichAllele1)
                        {
                            oneisthere = true;
                            break;
                        }
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichGene1))
                    {
                        if (GL.AlleleName == WhichAllele1)
                        {
                            oneisthere = true;
                            break;
                        }
                    }

                }
            }


            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichGene2))
                    {
                        if (GL.AlleleName == WhichAllele2)
                        {
                            twoisthere = true;
                            break;
                        }
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichGene2))
                    {
                        if (GL.AlleleName == WhichAllele2)
                        {
                            twoisthere = true;
                            break;
                        }
                    }

                }
            }

            if (oneisthere && twoisthere)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Overload of AlleleHeterozygous that takes two GeneLocus objects.
        /// Checks if both specified loci (by gene name and allele) are present
        /// somewhere in the organism's genome.
        /// </summary>
        public bool AlleleHeterozygous(GeneLocus WhichLocus1, GeneLocus WhichLocus2)
        {
            bool oneisthere = false;
            bool twoisthere = false;
            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichLocus1))
                    {
                        if (GL.AlleleName == WhichLocus1.AlleleName)
                        {
                            oneisthere = true;
                            break;
                        }
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichLocus1))
                    {
                        if (GL.AlleleName == WhichLocus1.AlleleName)
                        {
                            oneisthere = true;
                            break;
                        }
                    }

                }
            }


            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichLocus2))
                    {
                        if (GL.AlleleName == WhichLocus2.AlleleName)
                        {
                            twoisthere = true;
                            break;
                        }
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.IsSameGene(WhichLocus2))
                    {
                        if (GL.AlleleName == WhichLocus2.AlleleName)
                        {
                            twoisthere = true;
                            break;
                        }
                    }

                }
            }

            if (oneisthere && twoisthere)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Sums the value of a specified trait across ALL Transgene alleles in the
        /// organism's entire genome (both chromosome sets). This aggregates contributions
        /// from potentially multiple transgene insertions at different loci.
        ///
        /// For example, GetTransgeneLevel("Cas9_male") sums the Cas9_male trait value
        /// from every Transgene locus in the organism, giving the total Cas9 activity
        /// level. The result is clamped to a maximum of 1.0.
        ///
        /// Used to determine:
        ///   - Germline Cas9 activity for gene drive homing during meiosis
        ///   - Maternal/paternal Cas9 deposition into embryos
        ///   - gRNA expression levels for each target gene
        /// </summary>
        /// <param name="whichtrait">The trait name to sum (e.g., "Cas9_male", "gRNA_TRA").</param>
        /// <returns>Aggregated trait level, clamped to [0, 1].</returns>
        public float GetTransgeneLevel(string whichtrait)
        {
            float level = 0F;
            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.AlleleName == "Transgene")
                    {
                        level += GL.GetOutTraitValue(whichtrait);
                    }
                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.AlleleName == "Transgene")
                    {
                        level += GL.GetOutTraitValue(whichtrait);
                    }
                }
            }

            if (level > 1.0F)
                return 1.0F;
            else
                return level;

        }

        /// <summary>
        /// Applies zygotic (post-fertilization, embryonic) CRISPR/Cas9 gene drive activity.
        /// This models the scenario where parentally-deposited Cas9 protein and gRNA act
        /// in the early embryo to cut and convert WT alleles on the offspring's own
        /// chromosomes.
        ///
        /// Unlike germline gene drive (which occurs during meiosis in the parent), zygotic
        /// activity operates on the offspring's somatic genome after fertilization.
        ///
        /// Process:
        ///   1. Read the parentally-deposited Cas9 level from ParentalFactors["Cas9"].
        ///   2. For each gRNA-target pair, read the deposited gRNA level.
        ///   3. For each autosomal chromosome pair (sex chromosomes are skipped), attempt
        ///      bidirectional homing: ListA homologs try to convert ListB, and vice versa.
        ///   4. The ZygoticHDRReduction parameter reduces HDR efficiency in the zygotic
        ///      context (default 0.99 = 99% reduction), reflecting the biological observation
        ///      that zygotic HDR is much less efficient than germline HDR.
        ///
        /// Note: The HDRReduction parameter is passed to CutAndHomeInto but due to a code
        /// ordering issue in that method (HomRepair is zeroed then overwritten), the reduction
        /// is not effectively applied there. The actual HDR reduction would need a code fix
        /// to take effect.
        /// </summary>
        /// <param name="ZygoticHDRReduction">Fractional reduction in HDR efficiency (0–1).
        /// 0.99 means HDR is reduced by 99% in the zygote compared to the germline.</param>
        public void ZygoticCas9Activity(float ZygoticHDRReduction)
        {

            float Cas9level;

            if (!this.ParentalFactors.TryGetValue("Cas9", out Cas9level))
            {
                Cas9level = 0;
            }


            if (Cas9level > 0)
            {
                for (int u = 0; u < Simulation.Target_cognate_gRNA.GetLength(0); u++)
                {
                    float gRNAlevel = 0;

                    if (!this.ParentalFactors.TryGetValue(Simulation.Target_cognate_gRNA[u, 1], out gRNAlevel))
                    {
                        continue;
                    }
                    string gRNAtarget = Simulation.Target_cognate_gRNA[u, 0];

                    // Attempt homing on ChromosomeListA using ListB as template
                    for (var c = 0; c < this.ChromosomeListA.Count; c++)
                    {
                        if (this.ChromosomeListA[c].IsSexChrom())
                        continue;  // Skip sex chromosomes

                        this.ChromosomeListA[c].CutAndHomeInto(this.ChromosomeListB[c], this.GetSex(), Cas9level, gRNAlevel, gRNAtarget, ZygoticHDRReduction);

                    }

                    // Attempt homing on ChromosomeListB using ListA as template
                    for (var c = 0; c < this.ChromosomeListB.Count; c++)
                    {
                        if (this.ChromosomeListB[c].IsSexChrom())
                        continue;  // Skip sex chromosomes

                        this.ChromosomeListB[c].CutAndHomeInto(this.ChromosomeListA[c], this.GetSex(), Cas9level, gRNAlevel, gRNAtarget, ZygoticHDRReduction);

                    }

                }
            }
        }

        /// <summary>
        /// Randomly swaps ChromosomeListA and ChromosomeListB to randomize which
        /// parental chromosome set is in which list. This prevents systematic bias
        /// from always having the paternal set in ListA and maternal in ListB.
        /// Uses a standard three-variable swap pattern (temp = A; A = B; B = temp).
        /// </summary>
        public void SwapChromLists()
        {
            List<Chromosome> TList;
            TList = this.ChromosomeListA;
            this.ChromosomeListA = this.ChromosomeListB;
            this.ChromosomeListB = TList;
        }

        #endregion
    }
}