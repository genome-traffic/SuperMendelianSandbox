using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace SMS
{
    class GeneLocus
    {
        // Basic fields & properties
        string genename;
        public string GeneName
        {
            get { return genename; }
            set { genename = value; }
        }

        string allelename;
        string[] possiblealleles = { "R1", "R2", "Transgene", "WT" };
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

        float GenePosition;

        Dictionary<string, float> Traits;
        public void AddToTraits(string name, float value)
        {
            this.Traits[name] = value;
        }
        public float GetOutTraitValue(string key)
        {
            float output = 0F;
            if (this.Traits.TryGetValue(key, out output))
                return output;
            else
                return 0F;
        }

        //Constructor
        public GeneLocus(string iGeneName, float iGenePosition, string iallelename)
        {
            this.GeneName = iGeneName;
            this.GenePosition = iGenePosition;
            this.allelename = iallelename;
            this.Traits = new Dictionary<string, float>();
        }

        //Copy Constructor
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

        public void InheritTraits(GeneLocus Parent)
        {
            this.Traits.Clear();
            foreach (var ParentTrait in Parent.Traits)
            {
                this.Traits.Add(ParentTrait.Key, ParentTrait.Value);
            }
        }

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

        public float RecFreq(GeneLocus Other)
        {
            var dis = Math.Abs(this.GenePosition - Other.GenePosition);
            
            if (dis > 0.5F)
                return 0.5F;
            else
                return dis;
        }

        public float Distance(GeneLocus Other)
        {
           return Math.Abs(this.GenePosition - Other.GenePosition);
        }

        public bool IsSameGene(GeneLocus Other)
        {
            if (this.GeneName == Other.GeneName)
                return true;
            else
                return false;
        }

        public bool IsSameGene(string Other)
        {
            if (this.GeneName == Other)
                return true;
            else
                return false;
        }

        public bool IsSameAllele(GeneLocus Other)
        {
            if (this.allelename == Other.allelename)
                return true;
            else
                return false;
        }

        public bool IsSameAllele(string Other)
        {
            if (this.allelename == Other)
                return true;
            else
                return false;
        }

    }
}
