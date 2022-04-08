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

        //New empry Chromosome
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

        //New Chromosome in Meiosis (more complex DRIVE, with  gRNA checker)
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
                float Cas9level = parent.GetTransgeneLevel("Cas9");
                if (Cas9level > 0)
                {
                    for (int u = 0; u < Simulation.Target_cognate_gRNA.GetLength(0); u++)
                    {
                    float gRNAlevel = parent.GetTransgeneLevel(Simulation.Target_cognate_gRNA[u, 1]);

                        for (var i = 0; i < HC1.GeneLocusList.Count; i++)
                        {
                            if (HC1.GeneLocusList[i].IsSameGene(HC2.GeneLocusList[i]))
                            {
                                if (HC1.GeneLocusList[i].IsSameGene(Simulation.Target_cognate_gRNA[u, 0]))
                                {
                                    if (HC1.GeneLocusList[i].IsSameAllele("WT"))
                                    {
                                        if (Cas9level >= (float)Shuffle.random.NextDouble() && gRNAlevel >= (float)Shuffle.random.NextDouble())
                                        {
                                            dynamic Hom_Repair = 0;
                                            dynamic Cons = 0;

                                            Hom_Repair = HC2.GeneLocusList[i].GetOutTraitValue("Hom_Repair");
                                            Cons = HC1.GeneLocusList[i].GetOutTraitValue("Conservation");
                                            
                                            if (Hom_Repair >= (float)Shuffle.random.NextDouble())
                                            {
                                                HC1.GeneLocusList[i].InheritAll(HC2.GeneLocusList[i]);
                                            }
                                            else
                                            {
                                                if (Cons >= (float)Shuffle.random.NextDouble())
                                                    HC1.GeneLocusList[i].AlleleName = "R2";
                                                else
                                                    HC1.GeneLocusList[i].AlleleName = "R1";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        
                        for (var i = 0; i < HC2.GeneLocusList.Count; i++)
                        {
                            if (HC1.GeneLocusList[i].IsSameGene(HC2.GeneLocusList[i]))
                            {
                                if (HC2.GeneLocusList[i].IsSameGene(Simulation.Target_cognate_gRNA[u, 0]))
                                {
                                    if (HC2.GeneLocusList[i].IsSameAllele("WT"))
                                    {
                                        if (Cas9level >= (float)Shuffle.random.NextDouble() && gRNAlevel >= (float)Shuffle.random.NextDouble())
                                        {
                                            dynamic Hom_Repair = 0;
                                            dynamic Cons = 0;

                                            Hom_Repair = HC1.GeneLocusList[i].GetOutTraitValue("Hom_Repair");
                                            Cons = HC2.GeneLocusList[i].GetOutTraitValue("Conservation");

                                            if (Hom_Repair >= (float)Shuffle.random.NextDouble())
                                            {
                                                HC2.GeneLocusList[i].InheritAll(HC1.GeneLocusList[i]);
                                            }
                                            else
                                            {
                                                
                                                if (Cons >= (float)Shuffle.random.NextDouble())
                                                    HC2.GeneLocusList[i].AlleleName = "R2";
                                                else
                                                    HC2.GeneLocusList[i].AlleleName = "R1";
                                            }
                                        }
                                    }
                                }
                            }
                        }

                    }
                }
                #endregion


                #region recombining the two homologous chroms to create new chrom
                bool listone = true;
                for (var i = 0; i < HC1.GeneLocusList.Count; i++)
                {
                     
                    if (i == 0)
                    {
                        if (Shuffle.random.Next(0, 2) != 0)
                        {
                            this.GeneLocusList.Add(new GeneLocus(HC1.GeneLocusList[i]));
                            listone = true;
                        }
                        else
                        {
                            this.GeneLocusList.Add(new GeneLocus(HC2.GeneLocusList[i]));
                            listone = false;
                        }
                    }
                    else
                    {
                        if (listone == true)
                        {
                            if (HC1.GeneLocusList[i].RecFreq(HC1.GeneLocusList[i - 1]) < (float)Shuffle.random.NextDouble())
                            {
                                this.GeneLocusList.Add(new GeneLocus(HC1.GeneLocusList[i]));
                            }
                            else
                            {
                                this.GeneLocusList.Add(new GeneLocus(HC2.GeneLocusList[i]));
                                listone = false;
                            }
                        }
                        else
                        {
                            if (HC2.GeneLocusList[i].RecFreq(HC2.GeneLocusList[i - 1]) < (float)Shuffle.random.NextDouble())
                            {
                                this.GeneLocusList.Add(new GeneLocus(HC2.GeneLocusList[i]));
                            }
                            else
                            {
                                this.GeneLocusList.Add(new GeneLocus(HC1.GeneLocusList[i]));
                                listone = true;
                            }
                        }
                    }



                }
            }
            #endregion

        }

        public bool IsSexChrom()
        {
            if (this.homologouspairname == "Sex")
                return true;
            else
                return false;
        }

    }
}
