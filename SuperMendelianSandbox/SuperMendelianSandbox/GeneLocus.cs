using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace SMS
{
    /// <summary>
    /// Represents a single gene locus on a chromosome — the smallest unit of genetic
    /// information in the simulation. Each locus has:
    ///   - A gene name (e.g., "TRA", "FFER", "MoY") identifying which gene it encodes.
    ///   - An allele name from the set {WT, Transgene, R1, R2} indicating the variant present.
    ///   - A gene position (float, in map units) used to compute recombination frequencies
    ///     between linked loci during meiosis.
    ///   - A dictionary of Traits mapping trait names to float values. Traits encode the
    ///     functional properties of this allele, such as Cas9 activity levels, gRNA expression,
    ///     homology-directed repair (HDR) efficiency, and sequence conservation level.
    ///
    /// Allele semantics:
    ///   WT        — Wild-type allele; the natural/unmodified version of the gene.
    ///   Transgene — The gene drive construct carrying Cas9, gRNA, and associated traits.
    ///   R1        — Resistance allele created by NHEJ when the cut site is not conserved.
    ///              Functionally similar to WT (still produces functional protein).
    ///   R2        — Resistance allele created by NHEJ when the cut site IS conserved.
    ///              Non-functional (loss-of-function) allele.
    /// </summary>
    class GeneLocus
    {
        // Basic fields & properties

        string genename;
        /// <summary>
        /// The name of the gene at this locus (e.g., "TRA", "FFER", "MoY").
        /// Used to match homologous loci across chromosomes during meiosis and
        /// gene drive activity.
        /// </summary>
        public string GeneName
        {
            get { return genename; }
            set { genename = value; }
        }

        string allelename;
        string[] possiblealleles = { "R1", "R2", "Transgene", "WT" };
        /// <summary>
        /// The allele variant present at this locus. Restricted to the set
        /// {"R1", "R2", "Transgene", "WT"}. Setting to any other value throws
        /// an ArgumentException. R1 = functional resistance allele, R2 = non-functional
        /// resistance allele, Transgene = gene drive construct, WT = wild-type.
        /// </summary>
        public string AlleleName
        {
            get { return allelename; }
            set {
                if (possiblealleles.Contains(value))
                    allelename = value;
                else
                    throw new ArgumentException("not an allele name");
            }
        }

        /// <summary>
        /// Position on the chromosome in map units. Used to calculate recombination
        /// frequency between adjacent loci during meiotic crossover. Two loci separated
        /// by distance d recombine with frequency min(d, 0.5).
        /// </summary>
        float GenePosition;

        /// <summary>
        /// Dictionary of trait name → value pairs encoding the functional properties of
        /// this allele. Common trait keys include:
        ///   "Cas9_male"     — Cas9 nuclease activity level in males (0–1)
        ///   "Cas9_female"   — Cas9 nuclease activity level in females (0–1)
        ///   "Cas9_maternal" — Maternal Cas9 deposition into embryo (0–1)
        ///   "Cas9_paternal" — Paternal Cas9 deposition into embryo (0–1)
        ///   "gRNA_TRA"      — gRNA expression targeting the TRA gene (0–1)
        ///   "gRNA_FFER"     — gRNA expression targeting the FFER gene (0–1)
        ///   "HomRepair_male"   — Homology-directed repair rate in males (0–1)
        ///   "HomRepair_female" — Homology-directed repair rate in females (0–1)
        ///   "Conservation"  — Probability that NHEJ produces a conserved (R2) vs
        ///                     functional (R1) resistance allele (0–1)
        /// </summary>
        Dictionary<string, float> Traits;

        /// <summary>
        /// Adds or updates a trait value for this locus. If the trait key already exists,
        /// its value is overwritten.
        /// </summary>
        /// <param name="name">Trait name (e.g., "Cas9_male", "HomRepair_female").</param>
        /// <param name="value">Trait value, typically in range [0, 1].</param>
        public void AddToTraits(string name, float value)
        {
            this.Traits[name] = value;
        }

        /// <summary>
        /// Retrieves the value of a specified trait. Returns 0 if the trait is not present,
        /// which is the safe default (no activity / no expression).
        /// </summary>
        /// <param name="key">The trait name to look up.</param>
        /// <returns>The trait value, or 0 if not found.</returns>
        public float GetOutTraitValue(string key)
        {
            float output = 0F;
            if (this.Traits.TryGetValue(key, out output))
                return output;
            else
                return 0F;
        }

        /// <summary>
        /// Primary constructor. Creates a new gene locus with the given gene name,
        /// chromosomal position, and allele type. Initializes an empty Traits dictionary.
        /// </summary>
        /// <param name="iGeneName">Gene identifier (e.g., "TRA").</param>
        /// <param name="iGenePosition">Map position on the chromosome (float).</param>
        /// <param name="iallelename">Initial allele ("WT", "Transgene", "R1", or "R2").
        /// Note: bypasses the AlleleName setter validation by writing directly to the
        /// backing field.</param>
        public GeneLocus(string iGeneName, float iGenePosition, string iallelename)
        {
            this.GeneName = iGeneName;
            this.GenePosition = iGenePosition;
            this.allelename = iallelename;
            this.Traits = new Dictionary<string, float>();
        }

        /// <summary>
        /// Copy constructor. Creates a deep copy of an existing GeneLocus, including
        /// a full copy of the Traits dictionary. Used during chromosome duplication,
        /// meiosis, and organism cloning to ensure independent mutation of copies.
        /// </summary>
        /// <param name="Old">The GeneLocus to copy.</param>
        public GeneLocus(GeneLocus Old)
        {
            this.allelename = Old.allelename;
            this.GeneName = Old.GeneName;
            this.GenePosition = Old.GenePosition;

            this.Traits = new Dictionary<string, float>();
            foreach (var OldTrait in Old.Traits)
            {
                this.Traits.Add(OldTrait.Key, OldTrait.Value);
            }
        }

        /// <summary>
        /// Copies only the Traits dictionary from a parent locus into this locus,
        /// replacing any existing traits. Does NOT copy gene name, allele name, or
        /// position. Used when a locus needs to adopt the functional properties of
        /// another allele without changing its identity.
        /// </summary>
        /// <param name="Parent">The source locus whose traits are copied.</param>
        public void InheritTraits(GeneLocus Parent)
        {
            this.Traits.Clear();
            foreach (var ParentTrait in Parent.Traits)
            {
                this.Traits.Add(ParentTrait.Key, ParentTrait.Value);
            }
        }

        /// <summary>
        /// Copies ALL properties (traits, gene name, allele name, and position) from a
        /// parent locus into this locus, fully replacing its identity. This is the
        /// mechanism by which homology-directed repair (HDR) converts a WT allele into
        /// a Transgene copy during gene drive homing — the entire locus is overwritten
        /// with the drive allele's data.
        /// </summary>
        /// <param name="Parent">The source locus whose complete data is copied.</param>
        public void InheritAll(GeneLocus Parent)
        {
            this.Traits.Clear();
            foreach (var ParentTrait in Parent.Traits)
            {
                this.Traits.Add(ParentTrait.Key, ParentTrait.Value);
            }

            this.GeneName = Parent.GeneName;
            this.allelename = Parent.allelename;
            this.GenePosition = Parent.GenePosition;
        }

        /// <summary>
        /// Calculates the recombination frequency between this locus and another locus
        /// based on their chromosomal positions. Uses a simplified mapping function:
        /// recombination frequency = min(|distance|, 0.5). A cap of 0.5 corresponds
        /// to free assortment (unlinked loci recombine 50% of the time). Loci closer
        /// together recombine proportionally less often.
        /// </summary>
        /// <param name="Other">The other locus to measure recombination against.</param>
        /// <returns>Recombination frequency in [0, 0.5].</returns>
        public float RecFreq(GeneLocus Other)
        {
            var dis = Math.Abs(this.GenePosition - Other.GenePosition);

            if (dis > 0.5F)
                return 0.5F;
            else
                return dis;
        }

        /// <summary>
        /// Returns the absolute physical distance (in map units) between this locus and
        /// another locus on the same chromosome. Unlike RecFreq, this does not cap at 0.5.
        /// </summary>
        /// <param name="Other">The other locus.</param>
        /// <returns>Absolute distance between the two loci.</returns>
        public float Distance(GeneLocus Other)
        {
           return Math.Abs(this.GenePosition - Other.GenePosition);
        }

        /// <summary>
        /// Checks whether this locus and another locus represent the same gene
        /// (by gene name comparison). Used to find corresponding loci on homologous
        /// chromosomes during gene drive cutting and recombination.
        /// </summary>
        public bool IsSameGene(GeneLocus Other)
        {
            if (this.GeneName == Other.GeneName)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Overload that checks whether this locus matches a gene name string.
        /// </summary>
        public bool IsSameGene(string Other)
        {
            if (this.GeneName == Other)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Checks whether this locus carries the same allele variant as another locus.
        /// Used to verify homozygosity or specific genotype conditions.
        /// </summary>
        public bool IsSameAllele(GeneLocus Other)
        {
            if (this.allelename == Other.allelename)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Overload that checks whether this locus carries a specific allele name string
        /// (e.g., "WT", "Transgene", "R1", "R2").
        /// </summary>
        public bool IsSameAllele(string Other)
        {
            if (this.allelename == Other)
                return true;
            else
                return false;
        }

    }
}
