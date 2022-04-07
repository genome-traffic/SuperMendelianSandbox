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
        public Population(string type)
        {
            this.Adults = new List<Organism>();
            this.Eggs = new List<Organism>();

            if (type == "no resistance")
            {
                for (int i = 0; i < 300; i++)
                {
                    this.Adults.Add(new Organism(GenerateWTFemale()));
                }
                for (int i = 0; i < 120; i++)
                {
                    this.Adults.Add(new Organism(GenerateWTMale()));
                }

                for (int i = 0; i < 60; i++)
                {
                    this.Adults.Add(new Organism(GenerateZPG_Aper1_DriveMale()));
                    this.Adults.Add(new Organism(GenerateZPG_AP2_DriveMale()));
                    this.Adults.Add(new Organism(GenerateZPG_CP_DriveMale()));
                }

            }
            else if (type == "resistance")
            {

                for (int i = 0; i < 300; i++)
                {
                    this.Adults.Add(new Organism(GenerateWTFemale()));
                }
                for (int i = 0; i < 120; i++)
                {
                    this.Adults.Add(new Organism(GenerateWTMale()));
                }

                for (int i = 0; i < 60; i++)
                {
                    this.Adults.Add(new Organism(GenerateZPG_Aper1_R1zpg_DriveMale()));
                    this.Adults.Add(new Organism(GenerateZPG_AP2_DriveMale()));
                    this.Adults.Add(new Organism(GenerateZPG_CP_DriveMale()));
                }

            }
            else
                throw new InvalidOperationException("Population not defined!");

            Shuffle.ShuffleList(this.Adults);
        }



        //---------------------- Define Organism Types -----------------------------------------------------


        public Organism GenerateWTFemale()
        {
            Organism WTFemale = new Organism();

            GeneLocus ZPGa = new GeneLocus("ZPG", 1F, "WT");
            ZPGa.AddToTraits("Conservation", 0.95F);
            ZPGa.AddToTraits("Hom_Repair", 0.96F);
            GeneLocus ZPGb = new GeneLocus("ZPG", 1F, "WT");
            ZPGb.AddToTraits("Conservation", 0.95F);
            ZPGb.AddToTraits("Hom_Repair", 0.96F);

            GeneLocus Aper1a = new GeneLocus("Aper1", 2F, "WT");
            Aper1a.AddToTraits("Conservation", 0.95F);
            Aper1a.AddToTraits("Hom_Repair", 0.96F);
            GeneLocus Aper1b = new GeneLocus("Aper1", 2F, "WT");
            Aper1b.AddToTraits("Conservation", 0.95F);
            Aper1b.AddToTraits("Hom_Repair", 0.96F);

            GeneLocus AP2a = new GeneLocus("AP2", 3F, "WT");
            AP2a.AddToTraits("Conservation", 0.95F);
            AP2a.AddToTraits("Hom_Repair", 0.96F);
            GeneLocus AP2b = new GeneLocus("AP2", 3F, "WT");
            AP2b.AddToTraits("Conservation", 0.95F);
            AP2b.AddToTraits("Hom_Repair", 0.96F);

            GeneLocus CPa = new GeneLocus("CP", 1F, "WT");
            CPa.AddToTraits("Conservation", 0.95F);
            CPa.AddToTraits("Hom_Repair", 0.96F);
            GeneLocus CPb = new GeneLocus("CP", 1F, "WT");
            CPb.AddToTraits("Conservation", 0.95F);
            CPb.AddToTraits("Hom_Repair", 0.96F);

            Chromosome ChromXa = new Chromosome("X", "Sex");
            Chromosome ChromXb = new Chromosome("X", "Sex");
            Chromosome Chrom2a = new Chromosome("2", "2");
            Chromosome Chrom2b = new Chromosome("2", "2");
            Chromosome Chrom3a = new Chromosome("3", "3");
            Chromosome Chrom3b = new Chromosome("3", "3");

            Chrom2a.GeneLocusList.Add(ZPGa);
            Chrom2a.GeneLocusList.Add(Aper1a);
            Chrom2a.GeneLocusList.Add(AP2a);

            Chrom2b.GeneLocusList.Add(ZPGb);
            Chrom2b.GeneLocusList.Add(Aper1b);
            Chrom2b.GeneLocusList.Add(AP2b);

            Chrom3a.GeneLocusList.Add(CPa);
            Chrom3b.GeneLocusList.Add(CPb);

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

        public Organism GenerateZPG_DriveMale()
        {
            Organism ZPG_Male = new Organism(GenerateWTMale());

            GeneLocus ZPG_d = new GeneLocus("ZPG", 1F, "Transgene");
            ZPG_d.AddToTraits("Cas9", 0.99F);
            ZPG_d.AddToTraits("Cas9_maternal", 0F);
            ZPG_d.AddToTraits("gRNA_ZPG", 1F);
            ZPG_d.AddToTraits("Hom_Repair", 0.96F);

            ZPG_Male.ModifyAllele("A", ZPG_d, "WT");

            return ZPG_Male;
        }

        public Organism GenerateZPG_Aper1_DriveMale()
        {
            Organism ZPG_Aper1_Male = new Organism(GenerateZPG_DriveMale());

            GeneLocus Aper1_d = new GeneLocus("Aper1", 2F, "Transgene");
            Aper1_d.AddToTraits("Cas9", 0F);
            Aper1_d.AddToTraits("gRNA_Aper1", 1F);
            Aper1_d.AddToTraits("Hom_Repair", 0.96F);

            ZPG_Aper1_Male.ModifyAllele("A", Aper1_d, "WT");

            return ZPG_Aper1_Male;
        }

        public Organism GenerateZPG_Aper1_R1zpg_DriveMale()
        {
            Organism ZPG_Aper1_Male = new Organism(GenerateZPG_DriveMale());

            GeneLocus Aper1_d = new GeneLocus("Aper1", 2F, "Transgene");
            Aper1_d.AddToTraits("Cas9", 0F);
            Aper1_d.AddToTraits("gRNA_Aper1", 1F);
            Aper1_d.AddToTraits("Hom_Repair", 0.96F);

            ZPG_Aper1_Male.ModifyAllele("A", Aper1_d, "WT");

            GeneLocus ZPG_R1 = new GeneLocus("ZPG", 1F, "R1");

            ZPG_Aper1_Male.ModifyAllele("B", ZPG_R1, "WT");

            return ZPG_Aper1_Male;
        }

        public Organism GenerateZPG_CP_DriveMale()
        {
            Organism ZPG_CP_Male = new Organism(GenerateZPG_DriveMale());

            GeneLocus CP_d = new GeneLocus("CP", 1F, "Transgene");
            CP_d.AddToTraits("Cas9", 0F);
            CP_d.AddToTraits("gRNA_CP", 1F);
            CP_d.AddToTraits("Hom_Repair", 0.96F);

            ZPG_CP_Male.ModifyAllele("A", CP_d, "WT");

            return ZPG_CP_Male;
        }

        public Organism GenerateZPG_AP2_DriveMale()
        {
            Organism ZPG_AP2_Male = new Organism(GenerateZPG_DriveMale());

            GeneLocus AP2_d = new GeneLocus("AP2", 3F, "Transgene");
            AP2_d.AddToTraits("Cas9", 0F);
            AP2_d.AddToTraits("gRNA_AP2", 1F);
            AP2_d.AddToTraits("Hom_Repair", 0.96F);

            ZPG_AP2_Male.ModifyAllele("A", AP2_d, "WT");

            return ZPG_AP2_Male;
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

        public void Grow(float MaternalHDRReduction)
        {
            foreach (Organism OM in this.Adults)
            {

                if (Shuffle.random.Next(0, 2) != 0)
                {
                    OM.SwapChromLists();
                }

                OM.EmbryonicCas9Activity(MaternalHDRReduction);

            }

        }

    }

}
