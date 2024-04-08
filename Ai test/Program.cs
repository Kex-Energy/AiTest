using System;
using System.Collections.Generic;
using System.Transactions;
using System.Xml.Linq;

namespace Ai_test
{
    class Consts
    {
        public static readonly double[] types = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
    }
    class TreeNode
    {
        public List<CardInfo> AiTable, HumanTable, Hand;
        public int CurrentMana;
        public int AiFace, HumanFace;
        public int HumanFaceDmg;
        public List<TreeNode> ChildsNodes { get; }
        public List<Action> Actions;

        public bool extendable = true;
        
        public int nodeVisits = 1;
        public double TablePoints = .0, AwardPoints = .0;
        

        public TreeNode(List<CardInfo> AiTable, List<CardInfo> HumanTable, int AiFace, int HumanFace, int HumanFaceDmg, List<CardInfo> Hand, int mana)
        {
            this.AiTable = AiTable.ConvertAll(x => new CardInfo(x));
            this.HumanTable = HumanTable.ConvertAll(x => new CardInfo(x));
            this.Hand = Hand.ConvertAll(x => new CardInfo(x));
            this.CurrentMana = mana;
            ChildsNodes = new List<TreeNode>();
            Actions = new List<Action>();
            this.AiFace = AiFace;
            this.HumanFace = HumanFace;
            this.HumanFaceDmg = HumanFaceDmg;
        }
        public TreeNode(TreeNode node)
        {
            this.AiTable = node.AiTable.ConvertAll(x => new CardInfo(x));
            this.HumanTable = node.HumanTable.ConvertAll(x => new CardInfo(x));
            this.Hand = node.Hand.ConvertAll(x => new CardInfo(x));
            this.CurrentMana = node.CurrentMana;
            this.AwardPoints = node.AwardPoints;
            ChildsNodes = new List<TreeNode>();
            Actions = new List<Action>();
        }

        public void insertChild(TreeNode node)
        {
            ChildsNodes.Add(node);
        }
    }

    abstract class Action
    {
        public abstract void Do(List<CardInfo> AiTable, List<CardInfo> HumanTable, List<CardInfo> Hand, ref int CurrentMana);
    }
    class DrawCreatureAction:Action
    {
        CardInfo CardDrawn;
        public DrawCreatureAction(CardInfo CardDrawn)
        {
            this.CardDrawn = CardDrawn;
        }

        override public void Do(List<CardInfo> AiTable, List<CardInfo> HumanTable, List<CardInfo> Hand, ref int CurrentMana)
        {
            AiTable.Add(CardDrawn);
            Hand.Remove(CardDrawn);
            CurrentMana -= CardDrawn.manacost;
            
        }
    }
    class DrawAoeSpellAction : Action
    {
        CardInfo CardDrawn;
        public DrawAoeSpellAction(CardInfo CardDrawn)
        {
            this.CardDrawn = CardDrawn;
        }
        override public void Do(List<CardInfo> AiTable, List<CardInfo> HumanTable, List<CardInfo> Hand, ref int CurrentMana)
        {
            Hand.Remove(CardDrawn);
            CurrentMana -= CardDrawn.manacost;
            foreach(CardInfo card in HumanTable)
            {
                card.hp -= CardDrawn.dmg;
            }
        }
    }
    class AttackAction: Action
    {
        int Target;
        int Attacker;
        public AttackAction(int target, int attacker)
        {
            Target = target;
            Attacker = attacker;
        }

        override public void Do(List<CardInfo> AiTable, List<CardInfo> HumanTable, List<CardInfo> Hand, ref int CurrentMana)
        {
            AiTable[Attacker].hp -= HumanTable[Target].dmg;
            HumanTable[Target].hp -= AiTable[Attacker].dmg;
            AiTable[Attacker].active = false;
        }
    }
    class AttackFaceAction: Action
    {
        int HumanFace;
        int HumanDmg;
        int Attacker;
        public AttackFaceAction(ref int HumanFace, int HumanDmg, int Attacker)
        {
            this.HumanFace = HumanFace;
            this.HumanDmg = HumanDmg;
            this.Attacker = Attacker;
        }
        override public void Do(List<CardInfo> AiTable, List<CardInfo> HumanTable, List<CardInfo> Hand, ref int CurrentMana)
        {
            AiTable[Attacker].hp -= HumanDmg;
            HumanFace -= AiTable[Attacker].dmg;
            AiTable[Attacker].active = false;
        }
    }
    class EndTurnAction: Action
    {
        public EndTurnAction() { }
        override public void Do(List<CardInfo> AiTable, List<CardInfo> HumanTable, List<CardInfo> Hand, ref int CurrentMana)
        {

        }
    }

    
    class CardInfo 
    {
        public int hp, dmg;
        public bool active;
        public int manacost;
        public int type;
        public CardInfo(int dmg, int hp, bool active, int manacost, int type)
        {
            this.hp = hp;
            this.dmg = dmg;
            this.active = active;
            this.manacost = manacost;
            this.type = type;
        }
        public CardInfo(CardInfo original)
        {
            hp = original.hp;
            dmg = original.dmg;
            active = original.active;
            manacost = original.manacost;
            type = original.type;
        }
    }
    class Ai
    {
        const int c = 1;
        TreeNode ActionTree;
        int totalVisits = 1;
        int WeightDmgAlly = 1, WeightHpAlly = 1, WeightDmgEnemy = 1, WeightHpEnemy = 1; // Multipliers
        int WeightKill = 10, WeightSummon = 0, WeightCast = 1, WeightHumanKill = 100, WeightPossipleDeath = 100;

        public Ai(List<CardInfo> AiTable, List<CardInfo> HumanTable, int AiFace, int HumanFace, int HumanFaceDmg, List<CardInfo> Hand, int CurrentMana)
        {
            ActionTree = new TreeNode(AiTable, HumanTable, AiFace, HumanFace, HumanFaceDmg, Hand, CurrentMana);
        }

        double UbcFormula(TreeNode node)
        {
            return (node.TablePoints + node.AwardPoints) + c * Math.Sqrt(Math.Log(totalVisits) / node.nodeVisits);
        }
        void CalculateTablePoints(TreeNode node)
        {
            foreach (CardInfo card in node.AiTable)
                node.TablePoints += card.dmg * WeightDmgAlly + card.hp * WeightHpAlly;
            foreach(CardInfo card in node.HumanTable)
                node.TablePoints -= card.dmg * WeightDmgEnemy + card.hp * WeightHpEnemy;
            
        }

        TreeNode Selection(TreeNode root)
        {
            TreeNode selected = root.ChildsNodes[0];
            double max = -999999999;
            foreach(TreeNode node in root.ChildsNodes)
            {

                double UBCpoints = UbcFormula(node);
                
                if(max < UBCpoints)
                {
                    max = UBCpoints;
                    selected = node;
                }
            }
            
            return selected;
        }

        void Extension(TreeNode root) // запихнуть изменения в действия и в симуляции сделать их
        {
            if (root.extendable)
            {
                foreach (CardInfo card in root.Hand)
                {
                    if (card.type == 0 && card.manacost <= root.CurrentMana && root.AiTable.Count < 7)
                    {
                        TreeNode node = new TreeNode(root);
                        node.Actions.Add(new DrawCreatureAction(card));
                        node.AwardPoints += WeightSummon;
                        root.insertChild(node);
                    }
                    if (card.type == 1 && card.manacost <= root.CurrentMana)
                    {
                        
                        TreeNode node = new TreeNode(root);
                        node.Actions.Add(new DrawAoeSpellAction(card));
                        node.AwardPoints += WeightCast;
                        root.insertChild(node);
                    }
                }
                for (int i = 0; i < root.AiTable.Count; i++)
                {
                    if (root.AiTable[i].active == true)
                        if (root.HumanTable.Count > 0)
                        {
                            for (int j = 0; j < root.HumanTable.Count; j++)
                            {
                                TreeNode node = new TreeNode(root);
                                node.Actions.Add(new AttackAction(j, i));
                                root.insertChild(node);
                            }
                        }
                        else
                        {
                            TreeNode node = new TreeNode(root);
                            node.Actions.Add(new AttackFaceAction(ref root.HumanFace,root.HumanFaceDmg,i));
                            root.insertChild(node);
                        }
                }
                TreeNode node1 = new TreeNode(root);
                node1.Actions.Add(new EndTurnAction());
                node1.extendable = false;
                root.insertChild(node1);

            }
        }
        
        void Simulate(TreeNode root, List<TreeNode> backtrack)
        {
            foreach(Action act in root.Actions)
            {
                act.Do(root.AiTable, root.HumanTable, root.Hand, ref root.CurrentMana);
            }

            for(int i = root.AiTable.Count - 1; i >= 0; i--)
            {
                if(root.AiTable[i].hp <= 0)
                {
                    root.AiTable.RemoveAt(i);
                }
            }
            for (int i = root.HumanTable.Count - 1; i >= 0; i--)
            {
                if (root.HumanTable[i].hp <= 0)
                {
                    root.HumanTable.RemoveAt(i);
                    root.AwardPoints += WeightKill;
                }
            }
            if(root.HumanFace<=0)
            {
                root.AwardPoints += WeightHumanKill;
            }

            CalculateTablePoints(root);
            foreach(TreeNode node in backtrack)
            {
                node.TablePoints += root.TablePoints;
                node.AwardPoints += root.AwardPoints;
            }
        }
        public void ProcessTurn()
        {
            int i = 10000;
            while(i != 0)
            {
                List<TreeNode> backtrack = new List<TreeNode>();
                i--;
                if (ActionTree.ChildsNodes.Count > 0)
                {
                    TreeNode node = Selection(ActionTree);
                    node.nodeVisits++;

                    while (node.ChildsNodes.Count > 0)
                    {
                        backtrack.Add(node);
                        node = Selection(node);
                        node.nodeVisits++;
                    }

                    Extension(node);
                    for (int j = 0; j < node.ChildsNodes.Count; j++)
                    {
                        Simulate(node.ChildsNodes[j], backtrack);
                    }
                    totalVisits++;
                }
                else
                {
                    Extension(ActionTree);
                    for (int j = 0; j < ActionTree.ChildsNodes.Count; j++)
                    {
                        Simulate(ActionTree.ChildsNodes[j], backtrack);
                    }
                    totalVisits++;
                }
            }
        }
        public TreeNode TakeBestTurn()
        {
            TreeNode node = ActionTree;

            while (node.ChildsNodes.Count > 0)
            {
                double max = node.ChildsNodes[0].nodeVisits;
                TreeNode maxNode = node.ChildsNodes[0];
                foreach (TreeNode child in node.ChildsNodes)
                {
                    if (child.nodeVisits > max)
                    {
                        maxNode = child;
                        max = child.nodeVisits;
                    }

                }
                node = maxNode;
            }
            return node;
        }
    }
    
    internal class Program
    {
        static void Main(string[] args)
        {
            List<CardInfo> Aitable = new List<CardInfo>();
            List<CardInfo> HumanTable = new List<CardInfo>();
            List<CardInfo> Hand = new List<CardInfo>();
            Aitable.Add(new CardInfo(1, 1, false, 1, 0));
            Aitable.Add(new CardInfo(1, 1, false, 1, 0));
            Aitable.Add(new CardInfo(1, 1, false, 1, 0));
            HumanTable.Add(new CardInfo(1, 1, true, 1, 0));
            HumanTable.Add(new CardInfo(3, 3, true, 1, 0));
            HumanTable.Add(new CardInfo(2, 2, true, 1, 0));
            HumanTable.Add(new CardInfo(4, 1, true, 1, 0));
            Hand.Add(new CardInfo(1, 1, true, 1, 1));
            Ai test = new(Aitable, HumanTable, 10, 10, 1, Hand, 1);
            test.ProcessTurn();
            TreeNode result = test.TakeBestTurn();
            foreach (CardInfo c in result.AiTable)
            {
                Console.WriteLine("{0} {1}\t",c.dmg,c.hp);
            }
            Console.WriteLine("\n");
            foreach (CardInfo c in result.HumanTable)
            {
                Console.WriteLine("{0} {1}\t", c.dmg, c.hp);
            }
            Console.WriteLine("\n");


        }
    }
}