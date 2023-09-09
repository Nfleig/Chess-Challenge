using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

public class MCTSNode
{
    public static double explorationWeight = 1.0f; // Determines how much we want to explore unvisited nodes vs. exploiting visited nodes
    public Move action; // The move that lead to this node
    public Dictionary<Move, MCTSNode> children; // The children of this node
    public MCTSNode parent; // The parent of this node
    public int numberOfTimesVisited; // The number of times this node has been visited
    public double averageValue; // The average value of this node

    /* 
     * The UCB is a function that determines how valuable it would be to
     * visit this node based its value and how many times we've visited it.
     * 
     * When exploring the tree, we always choose the node with the highest UCB value
     */
    public double UCB { get {
            var logNParent = Math.Log(parent.numberOfTimesVisited);
            return averageValue + explorationWeight * Math.Sqrt(logNParent / numberOfTimesVisited);
    } }

    public MCTSNode(Move action, Dictionary<Move, MCTSNode> children, MCTSNode parent, int numberOfTimesVisited)
    {
        this.action = action;
        this.children = children;
        this.parent = parent;
        this.numberOfTimesVisited = numberOfTimesVisited;
    }
}

public class MyBot : IChessBot
{
    private MCTSNode _rootNode;
    private Random _random;
    private const int NUM_ROLLOUTS = 1;
    private const int SIMULATION_DEPTH = 1;

    public Move Think(Board board, Timer timer)
    {
        _random = new();
        Move[] moves = board.GetLegalMoves();
        return moves[_random.Next(moves.Length)];
    }

    private void RunMCTS(Board board, Timer timer, int timeForTurn)
    {
        while (timer.MillisecondsElapsedThisTurn < timeForTurn)
        {
            MCTSNode selectedNode = Select(_rootNode, board);
        }
    }

    private MCTSNode Select(MCTSNode currentNode, Board board)
    {
        Move[] moves = board.GetLegalMoves();
        if (moves.Length == currentNode.children.Count)
        {
            MCTSNode bestNode = currentNode.children.First().Value;
            double bestUCB = bestNode.UCB;

            foreach (MCTSNode node in currentNode.children.Values)
            {
                double nodeUCB = node.UCB;

                if (nodeUCB > bestUCB)
                {
                    bestNode = node;
                    bestUCB = nodeUCB;
                }
            }
            board.MakeMove(bestNode.action);
            var selectedNode = Select(bestNode, board);
            board.UndoMove(bestNode.action);
            return selectedNode;
        }
        else
        {
            var allMoves = board.GetLegalMoves();
            var expandMove = allMoves[_random.NextInt64(allMoves.Length)];

            float totalFitness = 0;
            board.MakeMove(expandMove);

            for (int i = 0; i < NUM_ROLLOUTS; i++)
                totalFitness += Simulate(board);

            currentNode.children.Add(expandMove, new MCTSNode(expandMove, new(), currentNode, 1));

            board.UndoMove(expandMove);

            return currentNode;
        }
    }

    private float Simulate(Board board)
    {
        var simulatedMoves = new Stack<Move>();
        while (simulatedMoves.Count < SIMULATION_DEPTH)
        {
            var moves = board.GetLegalMoves();

            if (moves.Length == 0)
                break;

            var randomMove = moves[_random.NextInt64(moves.Length)];
            simulatedMoves.Push(randomMove);
            board.MakeMove(randomMove);
        }

        var fitness = CalculateFitness(board);

        while (simulatedMoves.Count > 0)
        {
            board.UndoMove(simulatedMoves.Pop());
        }

        return fitness;
    }

    private float CalculateFitness(Board board)
    {
        var pieceLists = board.GetAllPieceLists();
        float fitness = 0;

        for (int i = 0; i < pieceLists.Length; i++)
        {
            fitness += i < 8 ? pieceLists[i].Count : -pieceLists[i].Count;
        }

        return fitness;
    }
}