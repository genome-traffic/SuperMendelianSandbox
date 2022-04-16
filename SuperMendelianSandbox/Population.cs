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
        public List<Organism> Adults
        {get;set;}
        public List<Organism> Eggs
        {get;set;}

        //---------------------- Population constructors  -----------------------------------------------------

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

            GeneLocus FFERa = new GeneLocus("FFER", 1F, "WT");
            FFERa.AddToTraits("Conservation", 1F);
            FFERa.AddToTraits("HomRepair_male", 0.959F);
            FFERa.AddToTraits("HomRepair_female", 0.994F);
            GeneLocus FFERb = new GeneLocus("FFER", 1F, "WT");
            FFERb.AddToTraits("Conservation", 1F);
            FFERb.AddToTraits("HomRepair_male", 0.959F);
            FFERb.AddToTraits("HomRepair_female", 0.994F);

            GeneLocus TRAa = new GeneLocus("TRA", 2F, "WT");
            TRAa.AddToTraits("Conservation", 0.90F);
            TRAa.AddToTraits("HomRepair_male", 0.95F);
            TRAa.AddToTraits("HomRepair_female", 0.95F);
            GeneLocus TRAb = new GeneLocus("TRA", 2F, "WT");
            TRAb.AddToTraits("Conservation", 0.90F);
            TRAb.AddToTraits("HomRepair_male", 0.95F);
            TRAb.AddToTraits("HomRepair_female", 0.994F);

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

            WTFemale.AddToParentalFactors("TRA_mRNA", 1F);

            return WTFemale;
        }

        public Organism GenerateWTMale()
        {
            Organism WTMale = new Organism(GenerateWTFemale());
            Chromosome ChromY = new Chromosome("Y", "Sex");
            GeneLocus MaleFactor = new GeneLocus("MoY", 1F, "WT");
            ChromY.GeneLocusList.Add(MaleFactor);

            WTMale.ChromosomeListA[0] = ChromY;

            return WTMale;
        }

        public Organism Generate_DriveMale()
        {
            Organism D_Male = new Organism(GenerateWTMale());

            GeneLocus FFD = new GeneLocus("FFER", 1F, "Transgene");
            FFD.AddToTraits("Cas9_male", 1F);
            FFD.AddToTraits("Cas9_female", 1F);
            FFD.AddToTraits("Cas9_maternal", 0F);
            FFD.AddToTraits("Cas9_paternal", 0F);
            FFD.AddToTraits("gRNA_FFER", 1F);
            FFD.AddToTraits("HomRepair_male", 0.959F);
            FFD.AddToTraits("HomRepair_female", 0.994F);

            D_Male.ModifyAllele("A", FFD, "WT");
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

    }

}
