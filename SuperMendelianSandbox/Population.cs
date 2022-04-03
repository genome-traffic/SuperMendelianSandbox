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

            GeneLocus ZPGa = new GeneLocus("ZPG", 1, "WT");
            ZPGa.Traits.Add("Conservation", 0.95F);
            ZPGa.Traits.Add("Hom_Repair", 0.95F);
            GeneLocus ZPGb = new GeneLocus("ZPG", 1, "WT");
            ZPGb.Traits.Add("Conservation", 0.95F);
            ZPGb.Traits.Add("Hom_Repair", 0.95F);

            GeneLocus Aper1a = new GeneLocus("Aper1", 2, "WT");
            Aper1a.Traits.Add("Conservation", 0.95F);
            Aper1a.Traits.Add("Hom_Repair", 0.95F);
            GeneLocus Aper1b = new GeneLocus("Aper1", 2, "WT");
            Aper1b.Traits.Add("Conservation", 0.95F);
            Aper1b.Traits.Add("Hom_Repair", 0.95F);

            GeneLocus AP2a = new GeneLocus("AP2", 3, "WT");
            AP2a.Traits.Add("Conservation", 0.95F);
            AP2a.Traits.Add("Hom_Repair", 0.95F);
            GeneLocus AP2b = new GeneLocus("AP2", 3, "WT");
            AP2b.Traits.Add("Conservation", 0.95F);
            AP2b.Traits.Add("Hom_Repair", 0.95F);

            GeneLocus CPa = new GeneLocus("CP", 1, "WT");
            CPa.Traits.Add("Conservation", 0.95F);
            CPa.Traits.Add("Hom_Repair", 0.95F);
            GeneLocus CPb = new GeneLocus("CP", 1, "WT");
            CPb.Traits.Add("Conservation", 0.95F);
            CPb.Traits.Add("Hom_Repair", 0.95F);

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

            GeneLocus ZPG_d = new GeneLocus("ZPG", 1, "Transgene");
            ZPG_d.Traits.Add("Cas9", 0.99F);
            ZPG_d.Traits.Add("Cas9_maternal", 0F);
            ZPG_d.Traits.Add("gRNA_ZPG", 1F);
            ZPG_d.Traits.Add("Hom_Repair", 0.96F);

            Organism.ModifyAllele(ref ZPG_Male.ChromosomeListA, ZPG_d, "WT");
            //Organism.ModifyAllele(ref ZPG_Male.ChromosomeListA, ZPG_d, "R1");

            return ZPG_Male;
        }

        public Organism GenerateZPG_Aper1_DriveMale()
        {
            Organism ZPG_Aper1_Male = new Organism(GenerateZPG_DriveMale());

            GeneLocus Aper1_d = new GeneLocus("Aper1", 2, "Transgene");
            Aper1_d.Traits.Add("Cas9", 0F);
            Aper1_d.Traits.Add("gRNA_Aper1", 1F);
            Aper1_d.Traits.Add("Hom_Repair", 0.96F);

            Organism.ModifyAllele(ref ZPG_Aper1_Male.ChromosomeListA, Aper1_d, "WT");

            //GeneLocus ZPG_R1 = new GeneLocus("ZPG", 1, "R1");

            //Organism.ModifyAllele(ref ZPG_Aper1_Male.ChromosomeListB, ZPG_R1, "WT");

            return ZPG_Aper1_Male;
        }

        public Organism GenerateZPG_Aper1_R1zpg_DriveMale()
        {
            Organism ZPG_Aper1_Male = new Organism(GenerateZPG_DriveMale());

            GeneLocus Aper1_d = new GeneLocus("Aper1", 2, "Transgene");
            Aper1_d.Traits.Add("Cas9", 0F);
            Aper1_d.Traits.Add("gRNA_Aper1", 1F);
            Aper1_d.Traits.Add("Hom_Repair", 0.96F);

            Organism.ModifyAllele(ref ZPG_Aper1_Male.ChromosomeListA, Aper1_d, "WT");

            GeneLocus ZPG_R1 = new GeneLocus("ZPG", 1, "R1");

            Organism.ModifyAllele(ref ZPG_Aper1_Male.ChromosomeListB, ZPG_R1, "WT");

            return ZPG_Aper1_Male;
        }

        public Organism GenerateZPG_CP_DriveMale()
        {
            Organism ZPG_CP_Male = new Organism(GenerateZPG_DriveMale());

            GeneLocus CP_d = new GeneLocus("CP", 2, "Transgene");
            CP_d.Traits.Add("Cas9", 0F);
            CP_d.Traits.Add("gRNA_CP", 1F);
            CP_d.Traits.Add("Hom_Repair", 0.99F);

            Organism.ModifyAllele(ref ZPG_CP_Male.ChromosomeListA, CP_d, "WT");

            return ZPG_CP_Male;
        }

        public Organism GenerateZPG_AP2_DriveMale()
        {
            Organism ZPG_AP2_Male = new Organism(GenerateZPG_DriveMale());

            GeneLocus AP2_d = new GeneLocus("AP2", 2, "Transgene");
            AP2_d.Traits.Add("Cas9", 0F);
            AP2_d.Traits.Add("gRNA_AP2", 1F);
            AP2_d.Traits.Add("Hom_Repair", 0.96F);

            Organism.ModifyAllele(ref ZPG_AP2_Male.ChromosomeListA, AP2_d, "WT");

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

    }

}
