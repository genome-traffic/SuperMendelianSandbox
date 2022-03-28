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

        public Dictionary<string, dynamic> MaternalFactors;

        //new organism (ex nihilo)
        public Organism()
        {
            this.ChromosomeListA = new List<Chromosome>();
            this.ChromosomeListB = new List<Chromosome>();
            this.MaternalFactors = new Dictionary<string, dynamic>();
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

            this.MaternalFactors = new Dictionary<string, dynamic>();

            foreach (var OldFactor in Old.MaternalFactors)
            {
                this.MaternalFactors.Add(OldFactor.Key, OldFactor.Value);
            }

        }

        //new organism (sex)
        public Organism(Organism Dad, Organism Mum)
        {
            this.ChromosomeListA = new List<Chromosome>();
            this.ChromosomeListB = new List<Chromosome>();

            this.ChromosomeListA.AddRange(Dad.GetGametChromosomeList());
            this.ChromosomeListB.AddRange(Mum.GetGametChromosomeList());

            //determine maternal factors
            this.MaternalFactors = new Dictionary<string, dynamic>();
            this.MaternalFactors.Add("Cas9", Mum.GetTransgeneLevel("Cas9_maternal"));

            for (int u = 0; u < Simulation.Target_cognate_gRNA.GetLength(0); u++)
            {
                this.MaternalFactors.Add(Simulation.Target_cognate_gRNA[u, 1], Mum.GetTransgeneLevel(Simulation.Target_cognate_gRNA[u, 1]));
            }
        }

        #region Organism methods

        public List<Chromosome> GetGametChromosomeList()
        {
            List<Chromosome> GametChroms = new List<Chromosome>();

            for (int i = 0; i < this.ChromosomeListA.Count; i++)
            {
                GametChroms.Add(new Chromosome(this.ChromosomeListA[i], this.ChromosomeListB[i], this));
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
            if (this.GetSex() == "female")
            {
                if (this.AlleleHomozygous("CP", "Transgene"))
                { fer = 0.9F; }
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

        public float GetTransgeneLevel(string whichtransgene)
        {
            float level = 0;
            foreach (Chromosome Chrom in this.ChromosomeListA)
            {
                foreach (GeneLocus GL in Chrom.GeneLocusList)
                {
                    if (GL.AlleleName == "Transgene")
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
                    if (GL.AlleleName == "Transgene")
                    {
                        foreach (var (name, value) in GL.Traits)
                        {
                            if (name == whichtransgene)
                                level += value;
                        }
                    }
                }
            }

            if (level > 1.0F)
                return 1.0F;
            else
                return level;

        }

        public void EmbryonicCas9Activity()
        {
            dynamic Cas9level = 0;

            if (!this.MaternalFactors.TryGetValue("Cas9", out Cas9level))
            {
                // the key isn't in the dictionary.
                return;
            }

            if (Cas9level > 0)
            {
                for (int u = 0; u < Simulation.Target_cognate_gRNA.GetLength(0); u++)
                {
                    dynamic gRNAlevel = 0;

                    if (!this.MaternalFactors.TryGetValue(Simulation.Target_cognate_gRNA[u, 1], out gRNAlevel))
                    {
                        // the key isn't in the dictionary.
                        continue;
                    }

                    for (var c = 0; c < this.ChromosomeListA.Count; c++)
                    {
                        if (this.ChromosomeListA[c].HomologousPairName == "Sex")
                        continue;

                        for (var i = 0; i < this.ChromosomeListA[c].GeneLocusList.Count; i++)
                        {
                            if (this.ChromosomeListA[c].GeneLocusList[i].GeneName == this.ChromosomeListB[c].GeneLocusList[i].GeneName)
                            {
                                if (this.ChromosomeListA[c].GeneLocusList[i].GeneName == Simulation.Target_cognate_gRNA[u, 0])
                                {
                                    if (this.ChromosomeListA[c].GeneLocusList[i].AlleleName == "WT")
                                    {
                                        //Console.WriteLine("WT in A ready for maternal modification!");
                                        if (Cas9level >= (float)Simulation.random.NextDouble() && gRNAlevel >= (float)Simulation.random.NextDouble())
                                        {
                                            dynamic Hom_Repair = 0;
                                            dynamic Cons = 0;

                                            this.ChromosomeListB[c].GeneLocusList[i].Traits.TryGetValue("Hom_Repair", out Hom_Repair);
                                            this.ChromosomeListA[c].GeneLocusList[i].Traits.TryGetValue("Conservation", out Cons);

                                            Hom_Repair = Hom_Repair * Simulation.MaternalHDRReduction;

                                            if (Hom_Repair >= (float)Simulation.random.NextDouble())
                                            {
                                                this.ChromosomeListA[c].GeneLocusList[i].AlleleName = this.ChromosomeListB[c].GeneLocusList[i].AlleleName;
                                                this.ChromosomeListA[c].GeneLocusList[i].InheritTraits(this.ChromosomeListB[c].GeneLocusList[i]);
                                            }
                                            else
                                            {
                                                //Console.WriteLine("List A maternal modification!");
                                                if (Cons >= (float)Simulation.random.NextDouble())
                                                    this.ChromosomeListA[c].GeneLocusList[i].AlleleName = "R2";
                                                else
                                                    this.ChromosomeListA[c].GeneLocusList[i].AlleleName = "R1";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    ///====================
                    for (var c = 0; c < this.ChromosomeListB.Count; c++)
                    {
                        if (this.ChromosomeListB[c].HomologousPairName == "Sex")
                            continue;

                        for (var i = 0; i < this.ChromosomeListB[c].GeneLocusList.Count; i++)
                        {
                            if (this.ChromosomeListA[c].GeneLocusList[i].GeneName == this.ChromosomeListB[c].GeneLocusList[i].GeneName)
                            {
                                if (this.ChromosomeListB[c].GeneLocusList[i].GeneName == Simulation.Target_cognate_gRNA[u, 0])
                                {
                                    if (this.ChromosomeListB[c].GeneLocusList[i].AlleleName == "WT")
                                    {
                                        //Console.WriteLine("WT in B ready for maternal modification!");

                                        if (Cas9level >= (float)Simulation.random.NextDouble() && gRNAlevel >= (float)Simulation.random.NextDouble())
                                        {
                                            dynamic Hom_Repair = 0;
                                            dynamic Cons = 0;

                                            this.ChromosomeListA[c].GeneLocusList[i].Traits.TryGetValue("Hom_Repair", out Hom_Repair);
                                            this.ChromosomeListB[c].GeneLocusList[i].Traits.TryGetValue("Conservation", out Cons);

                                            Hom_Repair = Hom_Repair * Simulation.MaternalHDRReduction;

                                            if (Hom_Repair >= (float)Simulation.random.NextDouble())
                                            {
                                                this.ChromosomeListB[c].GeneLocusList[i].AlleleName = this.ChromosomeListA[c].GeneLocusList[i].AlleleName;
                                                this.ChromosomeListB[c].GeneLocusList[i].InheritTraits(this.ChromosomeListA[c].GeneLocusList[i]);
                                            }
                                            else
                                            {
                                                //Console.WriteLine("List B maternal modification!");
                                                if (Cons >= (float)Simulation.random.NextDouble())
                                                    this.ChromosomeListB[c].GeneLocusList[i].AlleleName = "R2";
                                                else
                                                    this.ChromosomeListB[c].GeneLocusList[i].AlleleName = "R1";
                                            }
                                        }
                                    }
                                }
                            }
                        }
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