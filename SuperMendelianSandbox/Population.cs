using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.IO;


namespace SMS
{
    class Population
    {
        public List<Organism> Adults;
        public List<Organism> Eggs;

       

        //---------------------- Population constructors Organisms -----------------------------------------------------

        //new WT population
        public Population(int number)
        {
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

        //new WT population defining males and females
        public Population(int numberfemales, int numbermales)
        {

            this.Adults = new List<Organism>();
            this.Eggs = new List<Organism>();

            for (int i = 0; i < numberfemales; i++)
            {
                this.Adults.Add(new Organism(GenerateWTFemale()));
            }
            for (int i = 0; i < numbermales; i++)
            {
                this.Adults.Add(new Organism(GenerateWTMale()));
            }
            Shuffle.ShuffleList(this.Adults);
        }

        //new population by merging populations or releases
        public Population(Population One,Population Two)
        {
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

        //new specific population
        public Population(string type, int number)
        {
            this.Adults = new List<Organism>();
            this.Eggs = new List<Organism>();

            if (type == "standard release")
            {
                for (int i = 0; i < number; i++)
                {
                    this.Adults.Add(new Organism(Generate_DriveMale()));
                }
            }
            else
            throw new InvalidOperationException("Intervention not defined!");

            Shuffle.ShuffleList(this.Adults);
        }


        //---------------------- Define Organism Types -----------------------------------------------------


        public Organism GenerateWTFemale()
        {
            Organism WTFemale = new Organism();

            GeneLocus FFERa = new GeneLocus("FFER", 1, "WT");
            FFERa.Traits.Add("Conservation", 0.90F);
            FFERa.Traits.Add("Hom_Repair", 0.95F);
            GeneLocus FFERb = new GeneLocus("FFER", 1, "WT");
            FFERb.Traits.Add("Conservation", 0.90F);
            FFERb.Traits.Add("Hom_Repair", 0.95F);

            GeneLocus TRAa = new GeneLocus("TRA", 2, "WT");
            TRAa.Traits.Add("Conservation", 0.90F);
            TRAa.Traits.Add("Hom_Repair", 0.95F);
            GeneLocus TRAb = new GeneLocus("TRA", 2, "WT");
            TRAb.Traits.Add("Conservation", 0.90F);
            TRAb.Traits.Add("Hom_Repair", 0.95F);

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

            WTFemale.ChromosomeListA.Add(ChromXa);
            WTFemale.ChromosomeListB.Add(ChromXb);
            WTFemale.ChromosomeListA.Add(Chrom2a);
            WTFemale.ChromosomeListB.Add(Chrom2b);
            WTFemale.ChromosomeListA.Add(Chrom3a);
            WTFemale.ChromosomeListB.Add(Chrom3b);

            return WTFemale;
        }

        public Organism GenerateWTMale()
        {
            Organism WTMale = new Organism(GenerateWTFemale());
            Chromosome ChromY = new Chromosome("Y", "Sex");
            GeneLocus MaleFactor = new GeneLocus("MaleDeterminingLocus", 1, "WT");
            ChromY.GeneLocusList.Add(MaleFactor);

            WTMale.ChromosomeListA[0] = ChromY;

            return WTMale;
        }

        public Organism Generate_DriveMale()
        {
            Organism D_Male = new Organism(GenerateWTMale());

            GeneLocus FFD = new GeneLocus("TRA", 1, "Transgene");
            FFD.Traits.Add("Cas9", 0.95F);
            FFD.Traits.Add("Cas9_maternal", 0F);
            FFD.Traits.Add("gRNA_TRA", 1F);
            FFD.Traits.Add("Hom_Repair", 0.95F);

            Organism.ModifyAllele(ref D_Male.ChromosomeListA, FFD, "WT");
            return D_Male;
        }


        //----------------------- Population methods ----------------------------------------------------

        

        public List<Organism> PerformCross(Organism Dad, Organism Mum, int GlobalEggsPerFemale)
        {
            int EggsPerFemale = GlobalEggsPerFemale;
            List<Organism> EggList = new List<Organism>();

            EggsPerFemale = (int)(EggsPerFemale * Dad.GetFertility() * Mum.GetFertility());

            for (int i = 0; i < EggsPerFemale; i++)
            {
                EggList.Add(new Organism(Dad, Mum));
            }

            return EggList;
        }



        public void ReproduceToEggs(float m,int cap, int GlobalEggsPerFemale)
        {
            Shuffle.ShuffleList(this.Adults);

            int EffectivePopulation = (int)((1 - m) * cap);

            int numb;
            foreach (Organism F1 in this.Adults)
            {
                if (F1.GetSex() == "male")
                {
                    continue;
                }
                else
                {
                    for (int a = 0; a < EffectivePopulation; a++)
                    {
                        numb = Shuffle.random.Next(0, this.Adults.Count);
                        if (this.Adults[numb].GetSex() == "male")
                        {
                            this.Eggs.AddRange(this.PerformCross(this.Adults[numb], F1, GlobalEggsPerFemale));
                            break;
                        }
                    }
                }

            }

            this.Adults.Clear();
            Shuffle.ShuffleList(this.Eggs);

        }

    }

}
