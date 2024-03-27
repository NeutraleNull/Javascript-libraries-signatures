using Esprima;
using Esprima.Ast;

namespace SharedCommonStuff;

public class FeatureExtractor
{
    private readonly FeatureExtraction _featureExtraction = new();

    public FeatureExtraction ExtractFeatures(string code, bool isModule)
    {
        var parser = new JavaScriptParser(new ParserOptions
        {
            Tolerant = true,
            AllowReturnOutsideFunction = true,
            RegExpParseMode = RegExpParseMode.Skip,
            Tokens = true
        });

        if (isModule)
        {
            var module = parser.ParseModule(code);
            VisitNode(module);
        }
        else
        {
            var program = parser.ParseScript(code);
            VisitNode(program);
        }
        
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
        if (previousNode.Type == Nodes.VariableDeclarator)
            functionName = GetFunctionName(previousNode.As<VariableDeclarator>());
                
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
        
        function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionName, function.FunctionName!));
        function.ExtractedFeatures.Add((ExtractedFeatureType.Async, function.IsAsync.ToString()));
        function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionArgumentCount, function.ArgumentCount.ToString()));
        foreach (var argumentValues in function.FixArgumentValues)
        {
            function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionArgumentLiterals, $"{argumentValues.valueType}:{argumentValues.value}"));
        }
        
        ContextDelegationHandler(functionExpression.Body, null, function);
        
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
            case VariableDeclarator variableDeclarator:
                return GetFunctionName(variableDeclarator.Id);
            default:
                return string.Empty;
        }
    }

    private void VisitFunctionDeclaration(FunctionDeclaration? functionDeclaration)
    {
        var function = new Function
        {
            FunctionName = functionDeclaration.Id.Name,
            ArgumentCount = (ushort)functionDeclaration.Params.Count,
            FixArgumentValues = functionDeclaration.Params.Where(x => x.Type is Nodes.AssignmentPattern or Nodes.Identifier).Select(x =>
            {
                if (x.Type != Nodes.AssignmentPattern) return (x.Type.ToString(), x.ToString());
                
                //TODO:fix this assignment != data
                var assignmentPattern = x.As<AssignmentPattern>().Right;
                return (assignmentPattern.Type.ToString(), assignmentPattern.ToString());
            }).ToList(),
            IsAsync = functionDeclaration.Async
        };

        function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionName, function.FunctionName!));
        function.ExtractedFeatures.Add((ExtractedFeatureType.Async, function.IsAsync.ToString()));
        function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionArgumentCount, function.ArgumentCount.ToString()));
        foreach (var argumentValues in function.FixArgumentValues)
        {
            function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionArgumentLiterals, $"{argumentValues.valueType}:{argumentValues.value}"));
        }

        ContextDelegationHandler(functionDeclaration.Body, null, function);
        
        _featureExtraction.Functions.Add(function);
    }

    /// <summary>
    /// This function checks the type of the provided node and delegates the parsing logic to the 
    /// next parser logic
    /// </summary>
    /// <param name="node"></param>
    /// <param name="previousNode"></param>
    /// <param name="function"></param>
    private void ContextDelegationHandler(Node? node, Node? previousNode, Function function)
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
                ParseIdentifier(node, previousNode, function);
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
                ParseMemberExpression(node, previousNode, function);
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
                ParseObjectExpression(node, previousNode, function);
                break;
            case Nodes.FunctionExpression:
                ParseFunctionExpression(node, previousNode, function);
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
            case Nodes.ThrowStatement:
                ParseThrowStatement(node, function);
                break;
            case Nodes.AccessorProperty:
                ParseAccessorProperty(node, previousNode, function);
                break;
            case Nodes.ForInStatement:
                ParseForInStatement(node, previousNode, function);
                break;
            case Nodes.SequenceExpression:
                ParseSequenceExpression(node, function);
                break;
            case Nodes.ConditionalExpression:
                ParseConditionalExpression(node, function);
                break;
            case Nodes.ThisExpression:
                ParseThisExpression(node, function);
                break;
            case Nodes.Property:
                ParseProperty(node, function);
                break;
            case Nodes.ContinueStatement:
                ParseContinueStatement(node, function);
                break;
            case Nodes.FunctionDeclaration:
                ParseFunctionDeclaration(node, function);
                break;
            case Nodes.ObjectPattern:
                ParseObjectPattern(node, function);
                break;
            case Nodes.ArrayPattern:
                ParseArrayPattern(node, function);
                break;
            case Nodes.SpreadElement:
                ParseSpreadElement(node, function);
                break;
            case Nodes.AssignmentPattern:
                ParseAssignmentPattern(node, function);
                break;
            case Nodes.ForOfStatement:
                ParseForOfStatement(node, function);
                break;
            case Nodes.YieldExpression:
                ParseYieldExpression(node, function);
                break;
            case Nodes.TemplateLiteral:
                ParseTemplateLiteral(node, function);
                break;
            case Nodes.RestElement:
                ParseRestElement(node, function);
                break;
            case Nodes.EmptyStatement:
                ParseEmptyStatement(node, function);
                break;
            case Nodes.DebuggerStatement:
                ParseDebuggerStatement(node, function);
                break;
            case Nodes.ChainExpression:
                ParseChainExpression(node, function);
                break;
            case Nodes.ClassDeclaration:
                ParseClassDeclaration(node, function);
                break;
            case Nodes.Decorator:
                ParseDecorator(node, function);
                break;
            case Nodes.ClassBody:
                ParseClassBody(node, function);
                break;
            case Nodes.MethodDefinition:
                ParseMethodDefinition(node, function);
                break;
            case Nodes.LabeledStatement:
                ParseLabelStatement(node, function);
                break;
            case Nodes.MetaProperty:
                break;
            case Nodes.ImportExpression:
                ParseImportExpression(node, function);
                break;
            case Nodes.ImportSpecifier:
                ParseImportSpecifier(node, function);
                break;
            case Nodes.ClassExpression:
                ParseClassExpression(node, function);
                break;
            default:
                Console.WriteLine("");
                Console.WriteLine("UNHANDLED!");
                Console.WriteLine(node.Type.ToString());
                Console.WriteLine("");
                break;
        }
    }

    private void ParseClassExpression(Node node, Function function)
    {
        var classExpression = node.As<ClassExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Types, nameof(ClassExpression)));
        //function.ExtractedFeatures.Add((ExtractedFeatureType.ClassDeclarationName, classExpression.Id?.Name ?? "")); ;
        foreach (var decorator in classExpression.Decorators)
        {
            ContextDelegationHandler(decorator, classExpression, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "BEGIN-CLASS-DECLARATION"));
        ContextDelegationHandler(classExpression.Body, classExpression, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "END-CLASS-DECLARATION"));
    }

    private void ParseImportExpression(Node node, Function function)
    {
        var importExpression = node.As<ImportExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ImportExpression)));
        ContextDelegationHandler(importExpression.Source, importExpression, function);
    }

    private void ParseImportSpecifier(Node node, Function function)
    {
        var importStatement = node.As<ImportSpecifier>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ImportSpecifier)));
        ContextDelegationHandler(importStatement.Imported, importStatement, function);
    }

    private void ParseLabelStatement(Node node, Function function)
    {
        var labelStatement = node.As<LabeledStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(LabeledStatement)));
        ContextDelegationHandler(labelStatement.Label, labelStatement, function);
        ContextDelegationHandler(labelStatement.Body, labelStatement, function);
    }

    private void ParseMethodDefinition(Node node, Function function)
    {
        var methodDefinition = node.As<MethodDefinition>();
        foreach (var decorator in methodDefinition.Decorators)
        {
            function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(Decorator)));
            ContextDelegationHandler(decorator.Expression, methodDefinition, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(MethodDefinition)));
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, $"METHOD-DEFINITION-KIND-{methodDefinition.Kind}"));
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, $"METHOD-DEFINITION-Key"));
        ContextDelegationHandler(methodDefinition.Key, methodDefinition, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "METHOD-DEFINITION-VALUE"));
        ContextDelegationHandler(methodDefinition.Value, methodDefinition, function);
    }

    private void ParseClassBody(Node node, Function function)
    {
        var classBody = node.As<ClassBody>();
        foreach (var classElement in classBody.Body)
        {
            ContextDelegationHandler(classElement, classBody, function);
        }
    }

    private void ParseDecorator(Node node, Function function)
    {
        var decorator = node.As<Decorator>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(Decorator)));
        ContextDelegationHandler(decorator.Expression, decorator, function);
    }

    private void ParseClassDeclaration(Node node, Function function)
    {
        var classDeclaration = node.As<ClassDeclaration>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Types, nameof(ClassDeclaration)));
        function.ExtractedFeatures.Add((ExtractedFeatureType.ClassDeclarationName, classDeclaration.Id?.Name ?? "")); ;
        foreach (var decorator in classDeclaration.Decorators)
        {
            ContextDelegationHandler(decorator, classDeclaration, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "BEGIN-CLASS-DECLARATION"));
        ContextDelegationHandler(classDeclaration.Body, classDeclaration, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "END-CLASS-DECLARATION"));
    }

    private void ParseChainExpression(Node node, Function function)
    {
        var chainExpression = node.As<ChainExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ChainExpression)));
        ContextDelegationHandler(chainExpression.Expression, chainExpression, function);
    }

    private void ParseDebuggerStatement(Node node, Function function)
    {
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(DebuggerStatement)));
    }

    private void ParseEmptyStatement(Node node, Function function)
    {
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(EmptyStatement)));
    }

    private void ParseRestElement(Node node, Function function)
    {
        var restElement = node.As<RestElement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(RestElement)));
        ContextDelegationHandler(restElement.Argument, restElement, function);
    }

    private void ParseTemplateLiteral(Node node, Function function)
    {
        var templateLiteral = node.As<TemplateLiteral>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "BEGIN-TEMPLATE-LITERAL"));
        foreach (var quasi in templateLiteral.Quasis)
        {
            function.ExtractedFeatures.Add((ExtractedFeatureType.Strings, quasi.Value.Raw));
        }

        foreach (var expression in templateLiteral.Expressions)
        {
            ContextDelegationHandler(expression, templateLiteral, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "END-TEMPLATE-LITERAL"));
    }

    private void ParseYieldExpression(Node node, Function function)
    {
        var yieldExpression = node.As<YieldExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(YieldExpression)));
        ContextDelegationHandler(yieldExpression.Argument, yieldExpression, function);
    }

    private void ParseForOfStatement(Node node, Function function)
    {
        var forOfStatement = node.As<ForOfStatement>();
        if (forOfStatement.Await)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Async, "AWAIT"));
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(ForOfStatement)));
        ContextDelegationHandler(forOfStatement.Left, forOfStatement, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "OF"));
        ContextDelegationHandler(forOfStatement.Right, forOfStatement, function);
        ContextDelegationHandler(forOfStatement.Body, forOfStatement, function);
    }

    private void ParseAssignmentPattern(Node node, Function function)
    {
        var assignmentPattern = node.As<AssignmentPattern>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "BEGIN-ASSIGNMENT-PATTERN"));
        ContextDelegationHandler(assignmentPattern.Left, assignmentPattern, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "="));
        ContextDelegationHandler(assignmentPattern.Right, assignmentPattern, function);
    }

    private void ParseSpreadElement(Node node, Function function)
    {
        var spreadElement = node.As<SpreadElement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(SpreadElement)));
        ContextDelegationHandler(spreadElement.Argument, spreadElement, function);
    }

    private void ParseArrayPattern(Node node, Function function)
    {
        var arrayPattern = node.As<ArrayPattern>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "BEGIN-ARRAY-PATTERN"));
        foreach (var property in arrayPattern.Elements)
        {
            ContextDelegationHandler(property, arrayPattern, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "END-ARRAY-PATTERN"));
    }

    private void ParseObjectPattern(Node node, Function function)
    {
        var objectPattern = node.As<ObjectPattern>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "BEGIN-OBJECT-PATTERN"));
        foreach (var property in objectPattern.Properties)
        {
            ContextDelegationHandler(property, objectPattern, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "END-OBJECT-PATTERN"));
    }

    private void ParseFunctionDeclaration(Node node, Function function)
    {
        var functionDeclaration = node.As<FunctionDeclaration>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(FunctionDeclaration)));
        if (functionDeclaration.Async)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Async, "ASYNC"));
        function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionName, functionDeclaration.Id?.Name ?? "ANONYMOUS"));
        function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionArgumentCount, functionDeclaration.Params.Count.ToString()));
        foreach (var param in functionDeclaration.Params)
        {
            if (param.Type == Nodes.Identifier) 
                function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionArgumentLiterals, param.As<Identifier>().Name));
            if (param.Type == Nodes.AssignmentExpression)
            {
                var assignmentExpression = param.As<AssignmentExpression>();
                if (assignmentExpression.Left.Type == Nodes.Identifier)
                    function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionArgumentLiterals,param.As<AssignmentExpression>().Left.As<Identifier>().Name));
            }
        }
    }

    private void ParseContinueStatement(Node node, Function function)
    {
        var continueStatement = node.As<ContinueStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(ContinueStatement)));
    }

    private void ParseProperty(Node node, Function function)
    {
        var property = node.As<Property>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(Property)));
        function.ExtractedFeatures.Add((ExtractedFeatureType.Types, property.Kind.ToString()));
        ContextDelegationHandler(property.Key, property, function);
        ContextDelegationHandler(property.Value, property, function);
    }

    private void ParseThisExpression(Node node, Function function)
    {
        var thisExpression = node.As<ThisExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ThisExpression)));
    }

    private void ParseConditionalExpression(Node node, Function function)
    { 
        var conditionalExpression = node.As<ConditionalExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ConditionalExpression)));
        ContextDelegationHandler(conditionalExpression.Test, conditionalExpression, function);
        ContextDelegationHandler(conditionalExpression.Consequent, conditionalExpression, function);
        ContextDelegationHandler(conditionalExpression.Alternate, conditionalExpression, function);
    }

    private void ParseSequenceExpression(Node node, Function function)
    {
        var sequenceExpression = node.As<SequenceExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(SequenceExpression)));
        foreach (var expression in sequenceExpression.Expressions)
        {
            ContextDelegationHandler(expression, sequenceExpression, function);
        }
    }

    private void ParseForInStatement(Node node, Node? previousNode, Function function)
    {
        var forInStatement = node.As<ForInStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(ForInStatement)));
        ContextDelegationHandler(forInStatement.Left, forInStatement, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "IN"));
        ContextDelegationHandler(forInStatement.Right, forInStatement, function);
        foreach (var childNode in forInStatement.ChildNodes)
        {
            ContextDelegationHandler(childNode, previousNode, function); 
        }
    }

    private void ParseAccessorProperty(Node node, Node? previousNode, Function function)
    {
        var accessorProperty = node.As<AccessorProperty>();
        
    }

    private void ParseThrowStatement(Node node, Function function)
    {
        var throwStatement = node.As<ThrowStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(ThrowStatement)));
        ContextDelegationHandler(throwStatement.Argument, throwStatement, function);
    }

    private void ParseAssignmentExpression(Node node, Function function)
    {
        var assignmentExpression = node.As<AssignmentExpression>();
        ContextDelegationHandler(assignmentExpression.Left, assignmentExpression, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, assignmentExpression.Operator.ToString()));
        ContextDelegationHandler(assignmentExpression.Right, assignmentExpression, function);
    }

    private void ParseArrayExpression(Node node, Function function)
    {
        var arrayExpression = node.As<ArrayExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "BEGIN-ARRAY"));
        foreach (var arrayElement in arrayExpression.Elements)
        {
            if (arrayElement != null)
                ContextDelegationHandler(arrayElement, arrayExpression, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "END-ARRAY"));
    }

    private void ParseUnaryExpression(Node node, Function function)
    {
        var unaryExpression = node.As<UnaryExpression>();
        // can be either typeOf .... or ... typeOf, depending on the unary operator
        if (unaryExpression.Prefix)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, unaryExpression.Operator.ToString()));
        
        ContextDelegationHandler(unaryExpression.Argument, unaryExpression, function);
        
        if (!unaryExpression.Prefix)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, unaryExpression.Operator.ToString()));
    }

    private void ParseFunctionExpression(Node node, Node? previousNode, Function function)
    {
        var functionExpression = node.As<FunctionExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(FeatureExtraction)));
        if (previousNode?.Type == Nodes.CallExpression)
        {
            if (functionExpression.Async) 
                function.ExtractedFeatures.Add((ExtractedFeatureType.Async, "ASYNC"));
            function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, "ANONYMOUS-FUNCTION-CALL"));
            foreach (var argument in functionExpression.Params)
                ContextDelegationHandler(argument, functionExpression, function);
            ContextDelegationHandler(functionExpression.Body, functionExpression, function);
        }
    }

    private void ParseObjectExpression(Node node, Node? previousNode, Function function)
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
            ContextDelegationHandler(childNode,  objectExpression, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "END-OBJECT"));
    }

    private void ParseLogicalExpression(Node node, Function function)
    {
        var logicalExpression = node.As<LogicalExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, logicalExpression.Operator.ToString()));
        ContextDelegationHandler(logicalExpression.Left, logicalExpression, function);
        ContextDelegationHandler(logicalExpression.Right, logicalExpression, function);
    }

    private void ParseBreakStatement(Node node, Function function)
    {
        var breakStatement = node.As<BreakStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(BreakStatement)));
        foreach (var childNode in breakStatement.ChildNodes)
        {
            ContextDelegationHandler(childNode, breakStatement, function);
        }
    }

    private void ParseSwitchCase(Node node, Function function)
    {
        var switchCase = node.As<SwitchCase>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(SwitchCase)));
        ContextDelegationHandler(switchCase.Test, switchCase, function);
        foreach (var statement in switchCase.Consequent)
        {
            if (statement != null) ContextDelegationHandler(statement, switchCase, function);
        }
    }

    private void ParseSwitchStatement(Node node, Function function)
    {
        var switchStatement = node.As<SwitchStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(SwitchStatement)));
        ContextDelegationHandler(switchStatement.Discriminant, switchStatement, function);
        foreach (var switchCase in switchStatement.Cases)
        {
            ParseSwitchCase(switchCase, function);
        }
    }

    private void ParseAwaitExpression(Node node, Function function)
    {
        var awaitExpression = node.As<AwaitExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Async, nameof(AwaitExpression)));
        ContextDelegationHandler(awaitExpression.Argument, awaitExpression, function);
    }

    private void ParseDoWhileStatement(Node node, Function function)
    {
        var doWhileStatement = node.As<DoWhileStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(DoWhileStatement)));
        ContextDelegationHandler(doWhileStatement.Body, doWhileStatement, function);
        ContextDelegationHandler(doWhileStatement.Test, doWhileStatement, function);
    }

    private void ParseWhileStatement(Node node, Function function)
    {
        var whileStatement = node.As<WhileStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(WhileStatement)));
        ContextDelegationHandler(whileStatement.Test, whileStatement, function);
        ContextDelegationHandler(whileStatement.Body, whileStatement, function);
    }

    private void ParseCatchClause(Node node, Function function)
    {
        var catchClause = node.As<CatchClause>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(CatchClause)));
        ContextDelegationHandler(catchClause.Param,catchClause, function);
        ContextDelegationHandler(catchClause.Body, catchClause, function);
    }

    private void ParseTryStatement(Node node, Function function)
    {
        var tryStatement = node.As<TryStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(TryStatement)));
        ContextDelegationHandler(tryStatement.Block, tryStatement, function);
        ContextDelegationHandler(tryStatement.Handler, tryStatement, function);
        //ContextDelegationHandler(tryStatement.Finalizer, function);
    }

    private void ParseVariableDeclarator(Node node, Function function)
    {
        var variableDeclarator = node.As<VariableDeclarator>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(VariableDeclarator)));
        ContextDelegationHandler(variableDeclarator.Id,  variableDeclarator, function);
        ContextDelegationHandler(variableDeclarator.Init, variableDeclarator, function);
    }

    private void ParseMemberExpression(Node node, Node? previousNode, Function function)
    {
        var memberExpression = node.As<MemberExpression>();
        ContextDelegationHandler(memberExpression.Object, memberExpression, function);
        if (memberExpression.Computed)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "COMPUTED"));
        ContextDelegationHandler(memberExpression.Property,memberExpression, function);
    }

    private void ParseBlockStatement(Node node, Function function)
    {
        var blockStatement = node.As<BlockStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "BEGIN-BLOCK"));
        foreach (var statement in blockStatement.Body)
        {
            if (statement != null)
                ContextDelegationHandler(statement, blockStatement, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, "END-BLOCK"));
    }

    private void ParseIfStatement(Node node, Function function)
    {
        var ifStatement = node.As<IfStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(IfStatement)));
        
        ContextDelegationHandler(ifStatement.Test, ifStatement, function);
        ContextDelegationHandler(ifStatement.Consequent,ifStatement, function);
    }

    private void ParseUpdateExpression(Node node, Function function)
    {
        var updateExpression = node.As<UpdateExpression>();
        function.ExtractedFeatures.Add(updateExpression.Prefix
            ? (ExtractedFeatureType.Syntax, "PREFIX-" + updateExpression.Operator)
            : (ExtractedFeatureType.Syntax, updateExpression.Operator.ToString()));
        ContextDelegationHandler(updateExpression.Argument, updateExpression, function);
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
        ContextDelegationHandler(binaryExpression.Left, binaryExpression, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, binaryExpression.Operator.ToString()));
        ContextDelegationHandler(binaryExpression.Right,binaryExpression, function);
    }

    private void ParseForStatement(Node node, Function function)
    {
        var forStatement = node.As<ForStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(forStatement)));
        
        ContextDelegationHandler(forStatement.Init, forStatement, function);
        ContextDelegationHandler(forStatement.Test,  forStatement, function);
        ContextDelegationHandler(forStatement.Update, forStatement, function);
        ContextDelegationHandler(forStatement.Body, forStatement, function);
    }

    private void ParseCallExpression(Node node, Function function)
    {
        var callExpression = node.As<CallExpression>();
        function.ExtractedFeatures.Add(callExpression.Optional
            ? (ExtractedFeatureType.Syntax, "OPTIONAL-" + nameof(CallExpression))
            : (ExtractedFeatureType.Syntax, nameof(CallExpression)));

        ContextDelegationHandler(callExpression.Callee,  callExpression, function);
        foreach (var argument in callExpression.Arguments)
            if (argument != null) ContextDelegationHandler(argument, callExpression,function);
    }

    private void ParseExpressionStatement(Node node, Function function)
    {
        var expressionStatement = node.As<ExpressionStatement>();
        ContextDelegationHandler(expressionStatement.Expression, expressionStatement, function);
    }

    private void ParseIdentifier(Node node, Node? previousNode, Function function)
    {
        var identifier = node.As<Identifier>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.VariableName, identifier.Name));
        
        if (previousNode?.Type == Nodes.VariableDeclarator) return;
        
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
        ContextDelegationHandler(newExpression.Callee, newExpression, function);
        foreach (var argument in newExpression.Arguments)
            if (argument != null) ContextDelegationHandler(argument, newExpression, function);
    }

    private void ParseVariableDeclaration(Node node, Function function)
    {
        var variableDeclaration = node.As<VariableDeclaration>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax,variableDeclaration.Kind.ToString()));
        foreach (var declaration in variableDeclaration.Declarations)
            if (declaration != null) ContextDelegationHandler(declaration, variableDeclaration, function);
    }

    private void ParseReturnStatement(Node node, Function function)
    {
        var returnStatement = node.As<ReturnStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax,nameof(ReturnStatement)));
        if (returnStatement.Argument != null)
            ContextDelegationHandler(returnStatement.Argument, returnStatement, function);
    }

    private void ParseArrowFunctionExpression(Node node, Function function)
    {
        var arrowFunctionExpression = node.As<ArrowFunctionExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ArrowFunctionExpression)));
        if (arrowFunctionExpression.Async) 
            function.ExtractedFeatures.Add((ExtractedFeatureType.Async,nameof(ArrowFunctionExpression)));
        foreach (var argument in arrowFunctionExpression.Params)
        {
            ContextDelegationHandler(argument, arrowFunctionExpression, function);
        }

        ContextDelegationHandler(arrowFunctionExpression.Body, arrowFunctionExpression, function);
    }
}