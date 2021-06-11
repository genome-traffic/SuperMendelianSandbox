using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace SMS
{
    class Organism
    {
        public List<Chromosome> ChromosomeListA;
        public List<Chromosome> ChromosomeListB;

        public static Random randomorg = new Random();

        //new organism (ex nihilo)
        public Organism()
        {
            this.ChromosomeListA = new List<Chromosome>();
            this.ChromosomeListB = new List<Chromosome>();
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


        }

        //new organism (sex)
        public Organism(Organism Dad, Organism Mum)
        {
            this.ChromosomeListA = new List<Chromosome>();
            this.ChromosomeListB = new List<Chromosome>();

            this.ChromosomeListA.AddRange(Dad.GetGametChromosomeList());
            this.ChromosomeListB.AddRange(Mum.GetGametChromosomeList());

        }

        #region Organism methods

        public List<Chromosome> GetGametChromosomeList()
        {
            List<Chromosome> GametChroms = new List<Chromosome>();

            for (int i = 0; i < this.ChromosomeListA.Count; i++)
            {
                //GametChroms.Add(new Chromosome(this.ChromosomeListA[i], this.ChromosomeListB[i], true, this.GetTransgeneLevel("transgene_Cas9")));
                GametChroms.Add(new Chromosome(this.ChromosomeListA[i], this.ChromosomeListB[i],this));
            }
            return GametChroms;
        }

        public static void ModifyAllele(ref List<Chromosome> ChromList, GeneLocus NewLocus, string Replace)
        {
            foreach (Chromosome Chrom in ChromList)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.GeneName == NewLocus.GeneName)
                    {
                        if (GL.AlleleName == Replace)
                        {
                            GL.AlleleName = NewLocus.AlleleName;
                            GL.GeneName = NewLocus.GeneName;
                            GL.GenePosition = NewLocus.GenePosition;

                            GL.Traits.Clear();
                            foreach (var NewTrait in NewLocus.Traits)
                            {
                                GL.Traits.Add(NewTrait.Key, NewTrait.Value);
                            }
                        }
                    }
                }
            }
        }

        public string GetSexChromKaryo()
        {
            string karyo = "";
          
            //role of sex chromosomes
            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                if (Chrom.HomologousPairName == "Sex")
                {
                    karyo = Chrom.ChromosomeName;
                    break;
                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                if (Chrom.HomologousPairName == "Sex")
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
                    if (GL.GeneName == "MaleDeterminingLocus" && GL.AlleleName == "WT")
                    sex = "male";
                    return sex;
                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.GeneName == "MaleDeterminingLocus" && GL.AlleleName == "WT")
                    sex = "male";
                    return sex;
                }
            }

            return sex;

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
                    if (GL.GeneName == WhichGene)
                    {
                        GT1 = GL.AlleleName;
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.GeneName == WhichGene)
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

            //recessive male fertility
            if (this.GetSex() == "male")
            {
                if (this.AlleleHomozygous("RCD1R", "R2"))
                { fer = 0.05F; }
            }

            return fer;
        }

        public bool AllelePresent(string WhichGene, string WhichAllele)
        {

            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.GeneName == WhichGene)
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
                    if (GL.GeneName == WhichGene)
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
                    if (GL.GeneName == WhichLocus.GeneName)
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
                    if (GL.GeneName == WhichLocus.GeneName)
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
                    if (GL.GeneName == WhichGene)
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
                    if (GL.GeneName == WhichGene)
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
                    if (GL.GeneName == WhichLocus.GeneName)
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
                    if (GL.GeneName == WhichLocus.GeneName)
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
                    if (GL.GeneName == WhichGene1)
                    {
                        if (GL.AlleleName == WhichAllele1)
                        { oneisthere = true; }
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.GeneName == WhichGene1)
                    {
                        if (GL.AlleleName == WhichAllele1)
                        { oneisthere = true; }
                    }

                }
            }


            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.GeneName == WhichGene2)
                    {
                        if (GL.AlleleName == WhichAllele2)
                        { twoisthere = true; }
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.GeneName == WhichGene2)
                    {
                        if (GL.AlleleName == WhichAllele2)
                        { twoisthere = true; }
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
                    if (GL.GeneName == WhichLocus1.GeneName)
                    {
                        if (GL.AlleleName == WhichLocus1.AlleleName)
                        { oneisthere = true; }
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.GeneName == WhichLocus1.GeneName)
                    {
                        if (GL.AlleleName == WhichLocus1.AlleleName)
                        { oneisthere = true; }
                    }

                }
            }


            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.GeneName == WhichLocus2.GeneName)
                    {
                        if (GL.AlleleName == WhichLocus2.AlleleName)
                        { twoisthere = true; }
                    }

                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.GeneName == WhichLocus2.GeneName)
                    {
                        if (GL.AlleleName == WhichLocus2.AlleleName)
                        { twoisthere = true; }
                    }

                }
            }

            if (oneisthere && twoisthere)
                return true;
            else
                return false;
        }

        public int GetTransgeneLevel(string whichtransgene)
        {
            int level = 0;
            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.AlleleName == "Construct")
                    {
                        foreach (var (name, value) in GL.Traits)
                        {
                            if (name == whichtransgene)
                                level += value;
                        }
                    }
                }
            }

            foreach (Chromosome Chrom in this.ChromosomeListB)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.AlleleName == "Construct")
                    {
                        foreach (var (name, value) in GL.Traits)
                        {
                            if (name == whichtransgene)
                                level += value;
                        }
                    }
                }
            }

            if (level > 100)
                return 100;
            else
                return level;
            
        }

        #endregion
    }
}