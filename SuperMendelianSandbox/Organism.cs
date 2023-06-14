using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace SMS
{
    class Organism
    {
        public List<Chromosome> ChromosomeListA
        {get;set;}
        public List<Chromosome> ChromosomeListB
        {get;set;}

        Dictionary<string, float> ParentalFactors;

        //new organism (ex nihilo)
        public Organism()
        {
            this.ChromosomeListA = new List<Chromosome>();
            this.ChromosomeListB = new List<Chromosome>();
            this.ParentalFactors = new Dictionary<string, float>();
        }

        //new organism (clone) 
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

        //new organism (sex)
        public Organism(Organism Dad, Organism Mum)
        {
            this.ChromosomeListA = new List<Chromosome>();
            this.ChromosomeListB = new List<Chromosome>();

            this.ChromosomeListA.AddRange(Dad.GetGametChromosomeList());
            this.ChromosomeListB.AddRange(Mum.GetGametChromosomeList());

            this.ParentalFactors = new Dictionary<string, float>();

            //determine parental factors

            #region Cas9/gRNA deposition
            float Cas9deposit = 0;
            Cas9deposit = Mum.GetTransgeneLevel("Cas9_maternal") + Dad.GetTransgeneLevel("Cas9_paternal");


            if (Cas9deposit > 1)
                Cas9deposit = 1;
            else if (Cas9deposit < 0)
                Cas9deposit = 0;

            this.ParentalFactors.Add("Cas9", Cas9deposit);

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

            this.ParentalFactors["TRA_mRNA"] = 0F;

            if (Mum.AllelePresent("TRA", "R1"))
            { this.ParentalFactors["TRA_mRNA"] = 1F; }
            else
            {
                if (!(Mum.AllelePresent("TRA", "WT")))
                { this.ParentalFactors["TRA_mRNA"] = 0F; }
                else
                {
                    float Cas9level = Mum.GetTransgeneLevel("Cas9_female");
                    float tragRNAlevel = Mum.GetTransgeneLevel("gRNA_TRA");

                    if (Cas9level >= (float)Shuffle.random.NextDouble() && tragRNAlevel >= (float)Shuffle.random.NextDouble())
                    { this.ParentalFactors["TRA_mRNA"] = 0F; }
                    else
                    { this.ParentalFactors["TRA_mRNA"] = 1F; }

                }
            }
            
            #endregion


        }

        #region Organism methods

        public void AddToParentalFactors(string name, float value)
        {
            this.ParentalFactors[name] = value;
        }

        public List<Chromosome> GetGametChromosomeList()
        {
            List<Chromosome> GametChroms = new List<Chromosome>();

            for (int i = 0; i < this.ChromosomeListA.Count; i++)
            {
                GametChroms.Add(new Chromosome(this.ChromosomeListA[i], this.ChromosomeListB[i], this));
            }
            return GametChroms;
        }

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

        public string GetSex()
        {
            string sex = "female";

            //role of sex chromosomes
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

            //role of transformer
            
            if (this.ParentalFactors["TRA_mRNA"] < 1F)
            { return "male"; }

            if (this.AllelePresent("TRA", "WT") || this.AllelePresent("TRA", "R1"))
            { return sex; }
            else
            { return "male"; }

        }

        public bool IsMale()
        {
            if (this.GetSex() == "male")
                return true;
            else
                return false;
        }

        public bool IsFemale()
        {
            if (this.GetSex() == "male")
                return false;
            else
                return true;
        }

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

        public float GetFertility()
        {
            float fer = 1.0F;

            // recessive female sterility
            if (this.GetSex() == "female")
            {
                if (this.AlleleHomozygous("FFER", "Transgene"))
                { fer = 0F; }
                else if (this.AlleleHomozygous("FFER", "R2"))
                { fer = 0F; }
                else if (this.AlleleHeterozygous("FFER", "Transgene", "FFER", "R2"))
                { fer = 0F; }
            }

            //if (fer < 0F)
            //{ fer = 0F; }
            //else if (fer > 1F)
            //{ fer = 1F; }

            return fer;
             
        }

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


                    for (var c = 0; c < this.ChromosomeListA.Count; c++)
                    {
                        if (this.ChromosomeListA[c].IsSexChrom())
                        continue;

                        this.ChromosomeListA[c].CutAndHomeInto(this.ChromosomeListB[c], this.GetSex(), Cas9level, gRNAlevel, gRNAtarget, ZygoticHDRReduction);
                       
                    }
                    
                    for (var c = 0; c < this.ChromosomeListB.Count; c++)
                    {
                        if (this.ChromosomeListB[c].IsSexChrom())
                        continue;

                        this.ChromosomeListB[c].CutAndHomeInto(this.ChromosomeListA[c], this.GetSex(), Cas9level, gRNAlevel, gRNAtarget, ZygoticHDRReduction);

                    }

                }
            }
        }

        public void SwapChromLists()
        {
            List<Chromosome> TList;
            TList = this.ChromosomeListA;
            this.ChromosomeListA = this.ChromosomeListB;
            this.ChromosomeListA = TList;
        }

        #endregion
    }
}