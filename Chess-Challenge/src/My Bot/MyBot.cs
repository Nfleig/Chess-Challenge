using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

public class MCTSNode
{
    public static double explorationWeight = 1.0f; // Determines how much we want to explore unvisited nodes vs. exploiting visited nodes
    public Move action; // The move that lead to this node
    public List<MCTSNode> children; // The children of this node
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

    public MCTSNode(Move action, List<MCTSNode> children, MCTSNode parent, int numberOfTimesVisited, float averageValue)
    {
        this.action = action;
        this.children = children;
        this.parent = parent;
        this.numberOfTimesVisited = numberOfTimesVisited;
        this.averageValue = averageValue;
    }
}

public class MyBot : IChessBot
{
    private MCTSNode _rootNode;
    private Random _random;
    private int _movesExpanded;
    private bool _isPlayerWhite;
    private const int NUM_ROLLOUTS = 20;
    private const int SIMULATION_DEPTH = 20;
    private readonly int[] PIECE_WEIGHTS = { 1, 3, 3, 5, 9, 0, 1, 3, 3, 5, 9 };

    public Move Think(Board board, Timer timer)
    {
        _isPlayerWhite = board.IsWhiteToMove;
        _rootNode = new MCTSNode(Move.NullMove, new(), null, 0, 0);
        _random = new Random();

        while (timer.MillisecondsElapsedThisTurn < 100)
        {
            Select(_rootNode, board);
        }

        _rootNode.children.Sort(ReverseCompareMCTSNodes);
        Console.WriteLine("Moves Expanded: {0}, Final Weight: {1}", _movesExpanded, _rootNode.children[0].averageValue);
        return _rootNode.children[0].action;
    }

    private float Select(MCTSNode currentNode, Board board)
    {
        Move[] moves = board.GetLegalMoves();

        currentNode.numberOfTimesVisited++;
        if (moves.Length == currentNode.children.Count)
        {
            if (moves.Length == 0)
            {
                return 0f;
            }

            MCTSNode bestNode = currentNode.children[0];
            double bestUCB = bestNode.UCB;

            foreach (MCTSNode node in currentNode.children)
            {
                double nodeUCB = node.UCB;

                if (nodeUCB > bestUCB)
                {
                    bestNode = node;
                    bestUCB = nodeUCB;
                }
            }

            board.MakeMove(bestNode.action);
            bestNode.numberOfTimesVisited++;

            var possibleOpponentMoves = new List<Move>(board.GetLegalMoves());

            if (possibleOpponentMoves.Count() == 0)
            {
                board.UndoMove(bestNode.action);

                bestNode.averageValue = bestNode.averageValue + 1 / bestNode.numberOfTimesVisited * (1 - bestNode.averageValue);
                return 1;
            }

            var opponentMove = possibleOpponentMoves[_random.Next(possibleOpponentMoves.Count())];

            MCTSNode opponentMoveNode = null;

            foreach (MCTSNode expandedMove in bestNode.children)
            {
                if (expandedMove.action == opponentMove)
                {
                    opponentMoveNode = expandedMove;
                    break;
                }
            }

            if (opponentMoveNode == null)
            {
                opponentMoveNode = new MCTSNode(opponentMove, new(), bestNode, 1, 0);

                bestNode.children.Add(opponentMoveNode);
            }

            board.MakeMove(opponentMoveNode.action);
            opponentMoveNode.numberOfTimesVisited++;

            var averageValue = Select(opponentMoveNode, board);

            opponentMoveNode.averageValue = -averageValue;

            bestNode.averageValue = bestNode.averageValue + 1 / bestNode.numberOfTimesVisited * (averageValue - bestNode.averageValue);

            board.UndoMove(opponentMoveNode.action);

            board.UndoMove(bestNode.action);
            return averageValue;
        }
        else
        {
            var allMoves = new List<Move>(moves);

            foreach (MCTSNode expandedNode in currentNode.children)
                allMoves.Remove(expandedNode.action);

            if (allMoves.Count() == 0)
                return CalculateFitness(board);

            var expandMove = allMoves[_random.Next(allMoves.Count)];

            float totalFitness = 0;
            board.MakeMove(expandMove);

            for (int i = 0; i < NUM_ROLLOUTS; i++)
                totalFitness += Simulate(board);

            _movesExpanded++;
            currentNode.children.Add(new MCTSNode(expandMove, new(), currentNode, 1, totalFitness));

            board.UndoMove(expandMove);

            return totalFitness;
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
        bool isOurTurn = board.IsWhiteToMove == _isPlayerWhite;

        // The color of the player whose turn it is (player or opponent)
        // 1 for white, -1 for black
        int playerColor = board.IsWhiteToMove ? 1 : -1;

        if (board.IsInCheckmate())
            return !isOurTurn ? 1 : -1;

        if (board.IsDraw())
            return 0;

        var pieceLists = board.GetAllPieceLists();
        float fitness = 0;

        for (int i = 0; i < 6; i++)
        {
            fitness += pieceLists[i].Count() * PIECE_WEIGHTS[i] * playerColor;
        }

        for (int i = 7; i < pieceLists.Length - 1; i++)
        {
            fitness += pieceLists[i].Count() * PIECE_WEIGHTS[i] * -playerColor;
        }

        // Normalize the piece score based on the maximum possible piece value
        fitness /= 39;

        return isOurTurn ? fitness : -fitness;
    }

    private int ReverseCompareMCTSNodes(MCTSNode node1, MCTSNode node2)
    {
        return -node1.averageValue.CompareTo(node2.averageValue);
    }
}