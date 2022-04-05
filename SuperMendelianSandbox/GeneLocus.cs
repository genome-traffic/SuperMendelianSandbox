using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace SMS
{
    class GeneLocus
    {
        // Properties
        public string GeneName;
        public float GenePosition;
        public string AlleleName;
      
        //public List<(string, int)> Traits;
        public Dictionary<string, dynamic> Traits;

        //Constructor
        public GeneLocus(string iGeneName, float iGenePosition, string iAlleleName)
        {
            this.GeneName = iGeneName;
            this.GenePosition = iGenePosition;
            this.AlleleName = iAlleleName;
            this.Traits = new Dictionary<string, dynamic>();
        }

        //Copy Constructor
        public GeneLocus(GeneLocus Old)
        {
            this.AlleleName = Old.AlleleName;
            this.GeneName = Old.GeneName;
            this.GenePosition = Old.GenePosition;

            this.Traits = new Dictionary<string, dynamic>();
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
    }
}
