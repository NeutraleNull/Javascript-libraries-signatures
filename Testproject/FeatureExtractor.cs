using System.Collections.Specialized;
using Esprima;
using Esprima.Ast;
using SharedCommonStuff;

public class FeatureExtractor
{
    private readonly FeatureExtraction _featureExtraction;

    public FeatureExtractor()
    {
        _featureExtraction = new FeatureExtraction();
    }

    public FeatureExtraction ExtractFeatures(string code)
    {
        var parser = new JavaScriptParser(new ParserOptions
        {
            Tolerant = true,
            RegExpParseMode = RegExpParseMode.Skip,
            Tokens = true
        });
        var program = parser.ParseScript(code);
        VisitNode(program);
        return _featureExtraction;
    }

    private void VisitNode(Node node, Node? previousNode = null)
    {
        switch (node)
        {
            case FunctionDeclaration functionDeclaration:
                VisitFunctionDeclaration(functionDeclaration);
                break;
            case FunctionExpression functionExpression:
                VisitFunctionExpression(functionExpression, previousNode);
                break;
            case AssignmentExpression assignmentExpression:
                VisitAssignmentExpression(assignmentExpression, previousNode);
                break;
        }

        foreach (var child in node.ChildNodes)
        {
            VisitNode(child, node);
        }
    }

    private void VisitAssignmentExpression(AssignmentExpression assignmentExpression, Node? _)
    {
        if (assignmentExpression.Right.Type != Nodes.IfStatement) return;
        
        var ifStatement = assignmentExpression.Right.As<IfStatement>();
        if (ifStatement.Consequent.Type == Nodes.FunctionExpression)
        {
            VisitFunctionExpression(ifStatement.Consequent.As<FunctionExpression>(), assignmentExpression);
            return;
        }

        if (ifStatement.Alternate?.Type == Nodes.FunctionExpression)
        {
            VisitFunctionExpression(ifStatement.Alternate.As<FunctionExpression>(), assignmentExpression);
        }
    }


    private void VisitFunctionExpression(FunctionExpression functionExpression, Node? previousNode)
    {
        if (previousNode?.Type is not (Nodes.AssignmentExpression or Nodes.ObjectExpression or Nodes.VariableDeclarator)) return;

        var functionName = string.Empty;
        
        if (previousNode.Type == Nodes.AssignmentExpression)
            functionName = GetFunctionName(previousNode.As<AssignmentExpression>().Left);
        if (previousNode.Type == Nodes.ObjectExpression)
            functionName = GetFunctionName(previousNode.As<ObjectExpression>());
        if (previousNode.Type == Nodes.VariableDeclaration)
            functionName = GetFunctionName(previousNode.As<VariableDeclarator>();
                
        var function = new Function
        {
            FunctionName = functionName,
            ArgumentCount = (ushort)functionExpression.Params.Count,
            FixArgumentValues = functionExpression.Params
                .Where(x => x.Type is Nodes.AssignmentPattern or Nodes.Identifier).Select(x =>
                {
                    if (x.Type != Nodes.AssignmentPattern) return (x.Type.ToString(), x.ToString());

                    var assignmentPattern = x.As<AssignmentPattern>().Right;
                    return (assignmentPattern.Type.ToString(), assignmentPattern.ToString());
                }).ToList(),
            IsAsync = functionExpression.Async
        };
        
        ContextDelegationHandler(functionExpression.Body, function);
        
        _featureExtraction.Functions.Add(function);
    }
    
    private string GetFunctionName(Node? node)
    {
        switch (node)
        {
            case MemberExpression memberExpression:
            {
                var objectName = GetFunctionName(memberExpression.Object);
                var propertyName = memberExpression.Property.ToString();
                return string.IsNullOrEmpty(objectName) ? propertyName : $"{objectName}.{propertyName}";
            }
            case Identifier identifier:
                return identifier.Name;
            case ObjectExpression objectExpression:
                return GetFunctionName(objectExpression.Properties.First(x => x.Type == Nodes.Identifier));
            case VariableDeclaration variableDeclaration:
                return GetFunctionName(variableDeclaration.Declarations.First(x => x.Type == Nodes.Identifier));
            default:
                return string.Empty;
        }
    }

    private void VisitFunctionDeclaration(FunctionDeclaration? functionDeclaration)
    {
        var function = new Function
        {
            FunctionName = functionDeclaration.Id?.Name,
            ArgumentCount = (ushort)functionDeclaration.Params.Count,
            FixArgumentValues = functionDeclaration.Params.Where(x => x.Type is Nodes.AssignmentPattern or Nodes.Identifier).Select(x =>
            {
                if (x.Type != Nodes.AssignmentPattern) return (x.Type.ToString(), x.ToString());
                
                var assignmentPattern = x.As<AssignmentPattern>().Right;
                return (assignmentPattern.Type.ToString(), assignmentPattern.ToString());
            }).ToList(),
            IsAsync = functionDeclaration.Async
        };

        ContextDelegationHandler(functionDeclaration, function);
        
        _featureExtraction.Functions.Add(function);
    }

    /// <summary>
    /// This function checks the type of the provided node and delegates the parsing logic to the 
    /// next parser logic
    /// </summary>
    /// <param name="node"></param>
    /// <param name="function"></param>
    private void ContextDelegationHandler(Node? node, Function function)
    {
        if (node == null) return;
        switch (node.Type)
        {
            case Nodes.ReturnStatement:
                ParseReturnStatement(node, function);
                break;
            case Nodes.ArrowFunctionExpression:
                ParseArrowFunctionExpression(node, function);
                break;
            case Nodes.VariableDeclaration:
                ParseVariableDeclaration(node, function);
                break;
            case Nodes.NewExpression:
                ParseExpression(node, function);
                break;
            case Nodes.Identifier:
                ParseIdentifier(node, function);
                break;
            case Nodes.Literal:
                ParseLiteral(node, function);
                break;
            case Nodes.ExpressionStatement:
                ParseExpressionStatement(node, function);
                break;
            case Nodes.CallExpression:
                ParseCallExpression(node, function);
                break;
            case Nodes.ForStatement:
                ParseForStatement(node, function);
                break;
            case Nodes.BinaryExpression:
                ParseBinaryExpression(node, function);
                break;
            case Nodes.UpdateExpression:
                ParseUpdateExpression(node, function);
                break;
            case Nodes.IfStatement:
                ParseIfStatement(node, function);
                break;
            case Nodes.BlockStatement:
                ParseBlockStatement(node, function);
                break;
            case Nodes.MemberExpression:
                ParseMemberExpression(node, function);
                break;
            case Nodes.VariableDeclarator:
                ParseVariableDeclarator(node, function);
                break;
            case Nodes.TryStatement:
                ParseTryStatement(node, function);
                break;
            case Nodes.CatchClause:
                ParseCatchClause(node, function);
                break;
            case Nodes.WhileStatement:
                ParseWhileStatement(node, function);
                break;
            case Nodes.DoWhileStatement:
                ParseDoWhileStatement(node, function);
                break;
            case Nodes.AwaitExpression:
                ParseAwaitExpression(node, function);
                break;
            case Nodes.SwitchStatement:
                ParseSwitchStatement(node, function);
                break;
            case Nodes.SwitchCase:
                ParseSwitchCase(node, function);
                break;
            case Nodes.BreakStatement:
                ParseBreakStatement(node, function);
                break;
            case Nodes.LogicalExpression:
                ParseLogicalExpression(node, function);
                break;
            case Nodes.ObjectExpression:
                ParseObjectExpression(node, function);
                break;
            case Nodes.FunctionExpression:
                ParseFunctionExpression(node, function);
                break;
            case Nodes.UnaryExpression:
                ParseUnaryExpression(node, function);
                break;
            case Nodes.ArrayExpression:
                ParseArrayExpression(node, function);
                break;
            case Nodes.AssignmentExpression:
                ParseAssignmentExpression(node, function);
                break;
            default:
                break;
        }
    }

    private void ParseAssignmentExpression(Node node, Function function)
    {
        var assignmentExpression = node.As<AssignmentExpression>();
        ContextDelegationHandler(assignmentExpression.Left, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, assignmentExpression.Operator.ToString()));
        ContextDelegationHandler(assignmentExpression.Right, function);
    }

    private void ParseArrayExpression(Node node, Function function)
    {
        var arrayExpression = node.As<ArrayExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "BEGIN-ARRAY"));
        foreach (var arrayElement in arrayExpression.Elements)
        {
            if (arrayElement != null)
                ContextDelegationHandler(arrayElement, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "END-ARRAY"));
    }

    private void ParseUnaryExpression(Node node, Function function)
    {
        var unaryExpression = node.As<UnaryExpression>();
        // can be either typeOf .... or ... typeOf, depending on the unary operator
        if (unaryExpression.Prefix)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, unaryExpression.Operator.ToString()));
        
        ContextDelegationHandler(unaryExpression.Argument, function);
        
        if (!unaryExpression.Prefix)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, unaryExpression.Operator.ToString()));
    }

    private void ParseFunctionExpression(Node node, Function function)
    {
        var functionExpression = node.As<FunctionExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(FeatureExtraction)));
    }

    private void ParseObjectExpression(Node node, Function function)
    {
        var objectExpression = node.As<ObjectExpression>();
        if (objectExpression.ChildNodes.IsEmpty())
        {
            function.ExtractedFeatures.Add((ExtractedFeatureType.Types, nameof(ObjectExpression)));
            return;
        }

        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "BEGIN-OBJECT"));
        foreach (var childNode in objectExpression.ChildNodes)
        {
            ContextDelegationHandler(childNode, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "END-OBJECT"));
    }

    private void ParseLogicalExpression(Node node, Function function)
    {
        var logicalExpression = node.As<LogicalExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, logicalExpression.Operator.ToString()));
        ContextDelegationHandler(logicalExpression.Left, function);
        ContextDelegationHandler(logicalExpression.Right, function);
    }

    private void ParseBreakStatement(Node node, Function function)
    {
        var breakStatement = node.As<BreakStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(BreakStatement)));
    }

    private void ParseSwitchCase(Node node, Function function)
    {
        var switchCase = node.As<SwitchCase>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(SwitchCase)));
        ContextDelegationHandler(switchCase.Test, function);
        foreach (var statement in switchCase.Consequent)
        {
            if (statement != null) ContextDelegationHandler(statement, function);
        }
    }

    private void ParseSwitchStatement(Node node, Function function)
    {
        var switchStatement = node.As<SwitchStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(SwitchStatement)));
        ContextDelegationHandler(switchStatement.Discriminant, function);
        foreach (var switchCase in switchStatement.Cases)
        {
            ParseSwitchCase(switchCase, function);
        }
    }

    private void ParseAwaitExpression(Node node, Function function)
    {
        var awaitExpression = node.As<AwaitExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Async, nameof(AwaitExpression)));
        ContextDelegationHandler(awaitExpression.Argument, function);
    }

    private void ParseDoWhileStatement(Node node, Function function)
    {
        var doWhileStatement = node.As<DoWhileStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(DoWhileStatement)));
        ContextDelegationHandler(doWhileStatement.Body, function);
        ContextDelegationHandler(doWhileStatement.Test, function);
    }

    private void ParseWhileStatement(Node node, Function function)
    {
        var whileStatement = node.As<WhileStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(WhileStatement)));
        ContextDelegationHandler(whileStatement.Test, function);
        ContextDelegationHandler(whileStatement.Body, function);
    }

    private void ParseCatchClause(Node node, Function function)
    {
        var catchClause = node.As<CatchClause>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(CatchClause)));
        ContextDelegationHandler(catchClause.Param, function);
        ContextDelegationHandler(catchClause.Body, function);
    }

    private void ParseTryStatement(Node node, Function function)
    {
        var tryStatement = node.As<TryStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(TryStatement)));
        ContextDelegationHandler(tryStatement.Block, function);
        ContextDelegationHandler(tryStatement.Handler, function);
        //ContextDelegationHandler(tryStatement.Finalizer, function);
    }

    private void ParseVariableDeclarator(Node node, Function function)
    {
        var variableDeclarator = node.As<VariableDeclarator>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(VariableDeclarator)));
        ContextDelegationHandler(variableDeclarator.Id, function);
        ContextDelegationHandler(variableDeclarator.Init, function);
    }

    private void ParseMemberExpression(Node node, Function function)
    {
        var memberExpression = node.As<MemberExpression>();
        ContextDelegationHandler(memberExpression.Object, function);
        ContextDelegationHandler(memberExpression.Property, function);
    }

    private void ParseBlockStatement(Node node, Function function)
    {
        var blockStatement = node.As<BlockStatement>();

        foreach (var statement in blockStatement.Body)
        {
            if (statement != null)
                ContextDelegationHandler(statement, function);
        }
    }

    private void ParseIfStatement(Node node, Function function)
    {
        var ifStatement = node.As<IfStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(IfStatement)));
        
        ContextDelegationHandler(ifStatement.Test, function);
        ContextDelegationHandler(ifStatement.Consequent, function);
    }

    private void ParseUpdateExpression(Node node, Function function)
    {
        var updateExpression = node.As<UpdateExpression>();
        function.ExtractedFeatures.Add(updateExpression.Prefix
            ? (ExtractedFeatureType.Syntax, "PREFIX-" + updateExpression.Operator)
            : (ExtractedFeatureType.Syntax, updateExpression.Operator.ToString()));
        ContextDelegationHandler(updateExpression.Argument, function);
    }

    private void ParseLiteral(Node node, Function function)
    {
        var literal = node.As<Literal>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Types, literal.TokenType.ToString()));
        function.ExtractedFeatures.Add(literal.TokenType == TokenType.StringLiteral
            ? (ExtractedFeatureType.Strings, literal.StringValue ?? string.Empty)
            : (ExtractedFeatureType.Literals, literal.Raw));
    }

    private void ParseBinaryExpression(Node node, Function function)
    {
        var binaryExpression = node.As<BinaryExpression>();
        ContextDelegationHandler(binaryExpression.Left, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, binaryExpression.Operator.ToString()));
        ContextDelegationHandler(binaryExpression.Right, function);
    }

    private void ParseForStatement(Node node, Function function)
    {
        var forStatement = node.As<ForStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(forStatement)));
        
        ContextDelegationHandler(forStatement.Init, function);
        ContextDelegationHandler(forStatement.Test, function);
        ContextDelegationHandler(forStatement.Update, function);
        ContextDelegationHandler(forStatement.Body, function);
    }

    private void ParseCallExpression(Node node, Function function)
    {
        var callExpression = node.As<CallExpression>();
        function.ExtractedFeatures.Add(callExpression.Optional
            ? (ExtractedFeatureType.Syntax, "OPTIONAL-" + nameof(CallExpression))
            : (ExtractedFeatureType.Syntax, nameof(CallExpression)));

        ContextDelegationHandler(callExpression.Callee, function);
        foreach (var argument in callExpression.Arguments)
            if (argument != null) ContextDelegationHandler(argument, function);
    }

    private void ParseExpressionStatement(Node node, Function function)
    {
        var expressionStatement = node.As<ExpressionStatement>();
        ContextDelegationHandler(expressionStatement.Expression, function);
    }

    private void ParseIdentifier(Node node, Function function)
    {
        var identifier = node.As<Identifier>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.VariableName, identifier.Name));
        if (JavascriptHelper.AsyncIdentifiers.Contains(identifier.Name))
        {
            function.ExtractedFeatures.Add((ExtractedFeatureType.Async, identifier.Name));
            return;
        }
        if (JavascriptHelper.HostEnvironmentObjects.Contains(identifier.Name))
        {
            function.ExtractedFeatures.Add((ExtractedFeatureType.HostEnvironmentObject, identifier.Name));
            return;
        }
        if (JavascriptHelper.ECMAScriptObjects.Contains(identifier.Name))
        {
            function.ExtractedFeatures.Add((ExtractedFeatureType.ECMAObject, identifier.Name));
            return;
        }
    }

    private void ParseExpression(Node node, Function function)
    {
        var newExpression = node.As<NewExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(NewExpression)));
        //TODO: save argcount of new expression somehow...
        ContextDelegationHandler(newExpression.Callee, function);
        foreach (var argument in newExpression.Arguments)
            if (argument != null) ContextDelegationHandler(argument, function);
    }

    private void ParseVariableDeclaration(Node node, Function function)
    {
        var variableDeclaration = node.As<VariableDeclaration>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax,variableDeclaration.Kind.ToString()));
        foreach (var declaration in variableDeclaration.Declarations)
            if (declaration != null) ContextDelegationHandler(declaration, function);
    }

    private void ParseReturnStatement(Node node, Function function)
    {
        var returnStatement = node.As<ReturnStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax,nameof(ReturnStatement)));
        if (returnStatement.Argument != null)
            ContextDelegationHandler(returnStatement.Argument, function);
    }

    private void ParseArrowFunctionExpression(Node node, Function function)
    {
        var arrowFunctionExpression = node.As<ArrowFunctionExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ArrowFunctionExpression)));
        if (arrowFunctionExpression.Async) 
            function.ExtractedFeatures.Add((ExtractedFeatureType.Async,nameof(ArrowFunctionExpression)));
        foreach (var argument in arrowFunctionExpression.Params)
        {
            ContextDelegationHandler(argument, function);
        }

        ContextDelegationHandler(arrowFunctionExpression.Body, function);
    }
}