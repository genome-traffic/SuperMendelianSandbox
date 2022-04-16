using System;
using System.Collections.Generic;
using System.Linq;

namespace SMS
{
    class Chromosome
    {
        string chromosomename;
        string homologouspairname;
        string[] possiblechromnames = { "1", "2", "3", "X", "Y" };
        string[] possiblechrompairnames = { "1", "2", "3", "Sex" };

        public List<GeneLocus> GeneLocusList
        {get;set;}
        
        public string HomologousPairName
        {
            get { return homologouspairname; }
            set
            {
                if (possiblechrompairnames.Contains(value))
                    homologouspairname = value;
                else
                    throw new ArgumentException("not a pair name");
            }
        }

        public string ChromosomeName
        {
            get { return chromosomename; }
            set
            {
                if (possiblechromnames.Contains(value))
                    chromosomename = value;
                else
                    throw new ArgumentException("not a chrom name");
            }
        }

        //New empty Chromosome
        public Chromosome(string CName, string PName)
        {
            this.ChromosomeName = CName;
            this.HomologousPairName = PName;
            this.GeneLocusList = new List<GeneLocus>();
        }

        //Clone a Chromosome
        public Chromosome(Chromosome Old)
        {
            this.ChromosomeName = Old.ChromosomeName;
            this.HomologousPairName = Old.HomologousPairName;
            this.GeneLocusList = new List<GeneLocus>();

            foreach (GeneLocus OldGL in Old.GeneLocusList)
            {
                GeneLocus NewGL = new GeneLocus(OldGL);
                GeneLocusList.Add(NewGL);
            }
        }

        //New Chromosome in Meiosis (with CRISPR, gene drive & gRNA checker)
        public Chromosome(Chromosome HomChrom1, Chromosome HomChrom2, Organism parent)
        {
            this.GeneLocusList = new List<GeneLocus>();

            if (HomChrom1.HomologousPairName != HomChrom2.HomologousPairName)
            { throw new System.ArgumentException("Not homologous Chromosomes", "warning"); }

            if (HomChrom1.HomologousPairName == "Sex")
            {
                if (Shuffle.random.Next(0, 2) != 0)
                {
                    this.ChromosomeName = HomChrom1.ChromosomeName;
                    this.HomologousPairName = HomChrom1.HomologousPairName;

                    foreach (GeneLocus OldGL in HomChrom1.GeneLocusList)
                    {
                        GeneLocus NewGL = new GeneLocus(OldGL);
                        GeneLocusList.Add(NewGL);
                    }
                }
                else
                {
                    this.ChromosomeName = HomChrom2.ChromosomeName;
                    this.HomologousPairName = HomChrom2.HomologousPairName;

                    foreach (GeneLocus OldGL in HomChrom2.GeneLocusList)
                    {
                        GeneLocus NewGL = new GeneLocus(OldGL);
                        GeneLocusList.Add(NewGL);
                    }

                }
            }
            else
            {
                this.ChromosomeName = HomChrom1.ChromosomeName;
                this.HomologousPairName = HomChrom1.HomologousPairName;

                Chromosome HC1 = new Chromosome(HomChrom1);
                Chromosome HC2 = new Chromosome(HomChrom2);

                #region Cas9 activity / homing at all loci

                float Cas9level = parent.GetTransgeneLevel("Cas9_" + parent.GetSex());

                if (Cas9level > 0)
                {
                    for (int u = 0; u < Simulation.Target_cognate_gRNA.GetLength(0); u++)
                    {
                        float gRNAlevel = parent.GetTransgeneLevel(Simulation.Target_cognate_gRNA[u, 1]);
                        string gRNAtarget = Simulation.Target_cognate_gRNA[u, 0];

                        HC1.CutAndHomeInto(HC2, parent.GetSex(), Cas9level, gRNAlevel, gRNAtarget, 0F);
                        HC2.CutAndHomeInto(HC1, parent.GetSex(), Cas9level, gRNAlevel, gRNAtarget, 0F);
                    }
                }
                #endregion

                this.GeneLocusList = new Chromosome(HC1, HC2).GeneLocusList;

            }

            
        }

        //New Chromosome by simple recombination
        public Chromosome(Chromosome HomChrom1, Chromosome HomChrom2)
        {
            this.ChromosomeName = HomChrom1.ChromosomeName;
            this.HomologousPairName = HomChrom1.HomologousPairName;
            this.GeneLocusList = new List<GeneLocus>();

            bool listone = true;
            for (var i = 0; i < HomChrom1.GeneLocusList.Count; i++)
            {

                if (i == 0)
                {
                    if (Shuffle.random.Next(0, 2) != 0)
                    {
                        this.GeneLocusList.Add(new GeneLocus(HomChrom1.GeneLocusList[i]));
                        listone = true;
                    }
                    else
                    {
                        this.GeneLocusList.Add(new GeneLocus(HomChrom2.GeneLocusList[i]));
                        listone = false;
                    }
                }
                else
                {
                    if (listone == true)
                    {
                        if (HomChrom1.GeneLocusList[i].RecFreq(HomChrom1.GeneLocusList[i - 1]) < (float)Shuffle.random.NextDouble())
                        {
                            this.GeneLocusList.Add(new GeneLocus(HomChrom1.GeneLocusList[i]));
                        }
                        else
                        {
                            this.GeneLocusList.Add(new GeneLocus(HomChrom2.GeneLocusList[i]));
                            listone = false;
                        }
                    }
                    else
                    {
                        if (HomChrom2.GeneLocusList[i].RecFreq(HomChrom2.GeneLocusList[i - 1]) < (float)Shuffle.random.NextDouble())
                        {
                            this.GeneLocusList.Add(new GeneLocus(HomChrom2.GeneLocusList[i]));
                        }
                        else
                        {
                            this.GeneLocusList.Add(new GeneLocus(HomChrom1.GeneLocusList[i]));
                            listone = true;
                        }
                    }
                }



            }
        }

        public bool IsSexChrom()
        {
            if (this.homologouspairname == "Sex")
                return true;
            else
                return false;
        }

        public void CutAndHomeInto(Chromosome SourceChrom, string sex, float Cas9level, float gRNAlevel,string gRNAtarget, float HDRReduction)
        {

            for (var i = 0; i < this.GeneLocusList.Count; i++)
            {
                if (this.GeneLocusList[i].IsSameGene(SourceChrom.GeneLocusList[i]))
                {
                    if (this.GeneLocusList[i].IsSameGene(gRNAtarget))
                    {
                        if (this.GeneLocusList[i].IsSameAllele("WT"))
                        {
                            if (Cas9level >= (float)Shuffle.random.NextDouble() && gRNAlevel >= (float)Shuffle.random.NextDouble())
                            {
                                float HomRepair = 0;
                                float Cons = 0;

                                HomRepair = HomRepair * (1 - HDRReduction);

                                HomRepair = SourceChrom.GeneLocusList[i].GetOutTraitValue("HomRepair_" + sex);
                                Cons = this.GeneLocusList[i].GetOutTraitValue("Conservation");

                                if (HomRepair >= (float)Shuffle.random.NextDouble())
                                {
                                    this.GeneLocusList[i].InheritAll(SourceChrom.GeneLocusList[i]);
                                }
                                else
                                {
                                    if (Cons >= (float)Shuffle.random.NextDouble())
                                        this.GeneLocusList[i].AlleleName = "R2";
                                    else
                                        this.GeneLocusList[i].AlleleName = "R1";
                                }
                            }
                        }
                    }
                }
            }

        }

    }
}
