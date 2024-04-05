using System.Runtime.CompilerServices;
using Acornima;
using Acornima.Ast;

namespace Infrastructure.Parser;

public class JavascriptFeatureExtractor
{
    private readonly List<Function> _extractedFunctions = new();

    /// <summary>
    /// This function sets up the Acornima AST parser and generates the AST.
    /// After that it will traverse the program node for its children and start the recursive search for
    /// functions and signatures. In the end we will have a full list of all found function with the extracted features
    /// from them. 
    /// </summary>
    /// <param name="code"></param>
    /// <param name="sourceFile"></param>
    /// <param name="isModule"></param>
    /// <returns></returns>
    public List<Function> ExtractFeatures(string code, string sourceFile, bool isModule)
    {
        var parser = new Acornima.Parser(new ParserOptions
        {
            RegExpParseMode = RegExpParseMode.Skip,
            Tolerant = true,
            AllowImportExportEverywhere = true,
            AllowReturnOutsideFunction = true,
            AllowAwaitOutsideFunction = true,
            AllowSuperOutsideMethod = true,
            AllowNewTargetOutsideFunction = true,
            CheckPrivateFields = false
        });
        Program program;
        if (isModule)
            program = parser.ParseScript(code, sourceFile);
        else
            program = parser.ParseModule(code, sourceFile);
        
        //Console.WriteLine(program.ToJsonString());

        foreach (var node in program.Body)
            VisitNode(node, null);

        return _extractedFunctions;
    }

    /// <summary>
    /// Main function node recursive searching. 
    /// </summary>
    /// <param name="node"></param>
    /// <param name="previousNode"></param>
    private void VisitNode(Node node, Node? previousNode)
    {
        //if (previousNode?.Type == NodeType.CallExpression) return;
        
        // There are three types of node types that can indicate a function is declared
        switch (node.Type)
        {
            case NodeType.FunctionDeclaration:
                var functionDeclaration = node.As<FunctionDeclaration>();
                var functionName = functionDeclaration.Id?.Name ?? "anonymous";
                var paramsCount = functionDeclaration.Params.Count;

                var function = new Function() { FunctionName = functionName, ArgumentCount = (ushort)paramsCount };
                _extractedFunctions.Add(function);
                if (functionDeclaration.Async)
                    function.ExtractedFeatures.Add((ExtractedFeatureType.Async, FunctionDataHelper.FunctionAsync));
                function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionName, functionName));
                function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionArgumentCount, paramsCount.ToString()));
                
                NodeDelegationHandler(functionDeclaration.Body, functionDeclaration, function);
                break;
            case NodeType.FunctionExpression:
                var functionExpression = node.As<FunctionExpression>();
                paramsCount = functionExpression.Params.Count;
                functionName = GetFunctionName(functionExpression, previousNode);
                function = new Function
                {
                    ArgumentCount = (ushort)paramsCount,
                    FunctionName = functionName
                };
                _extractedFunctions.Add(function);
                if (functionExpression.Async)
                    function.ExtractedFeatures.Add((ExtractedFeatureType.Async, FunctionDataHelper.FunctionAsync));
                function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionName, functionName));
                function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionArgumentCount, paramsCount.ToString()));
                
                NodeDelegationHandler(functionExpression.Body, functionExpression, function);
                break;
            case NodeType.ArrowFunctionExpression:
                var arrowFunctionExpression = node.As<ArrowFunctionExpression>();
                paramsCount = arrowFunctionExpression.Params.Count;
                functionName = GetFunctionName(arrowFunctionExpression, previousNode);
                function = new Function
                {
                    ArgumentCount = (ushort)paramsCount,
                    FunctionName = functionName
                };
                _extractedFunctions.Add(function);
                if (arrowFunctionExpression.Async)
                    function.ExtractedFeatures.Add((ExtractedFeatureType.Async, FunctionDataHelper.FunctionAsync));
                function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionName, functionName));
                function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionArgumentCount, paramsCount.ToString()));
                
                NodeDelegationHandler(arrowFunctionExpression.Body, arrowFunctionExpression, function);
                break;
            default:
                // for all other types we will try to traverse their children recursively 
                foreach (var childNode in node.ChildNodes)
                {
                        VisitNode(childNode, node);
                }

                break;
        }
    }


    /// <summary>
    /// This handler will try to delegate the work of handling the nodes.
    /// It is basically the great postal service.
    /// Previous node is required by some handlers to determine the correct feature to extract.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="previousNode"></param>
    /// <param name="function"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private void NodeDelegationHandler(Node node, Node previousNode, Function function)
    {
        switch (node.Type)
        {
            case NodeType.Unknown:
                HandleUnknownType(node, previousNode, function);
                break;
            case NodeType.AccessorProperty:
                HandleAccessorProperty(node, previousNode, function);
                break;
            case NodeType.ArrayExpression:
                HandleArrayExpression(node, previousNode, function);
                break;
            case NodeType.ArrayPattern:
                HandleArrayPattern(node, previousNode, function);
                break;
            case NodeType.ArrowFunctionExpression:
                HandleArrowFunctionExpression(node, previousNode, function);
                break;
            case NodeType.AssignmentExpression:
                HandleAssignmentExpression(node, previousNode, function);
                break;
            case NodeType.AssignmentPattern:
                HandleAssignmentPattern(node, previousNode, function);
                break;
            case NodeType.AwaitExpression:
                HandleAwaitExpression(node, previousNode, function);
                break;
            case NodeType.BinaryExpression:
                HandleBinaryExpression(node, previousNode, function);
                break;
            case NodeType.BlockStatement:
                HandleBlockStatement(node, previousNode, function);
                break;
            case NodeType.BreakStatement:
                HandleBreakStatement(node, previousNode, function);
                break;
            case NodeType.CallExpression:
                HandleCallExpression(node, previousNode, function);
                break;
            case NodeType.CatchClause:
                HandleCatchClause(node, previousNode, function);
                break;
            case NodeType.ChainExpression:
                HandleChainExpression(node, previousNode, function);
                break;
            case NodeType.ClassBody:
                HandleClassBody(node, previousNode, function);
                break;
            case NodeType.ClassDeclaration:
                HandleClassDeclaration(node, previousNode, function);
                break;
            case NodeType.ClassExpression:
                HandleClassExpression(node, previousNode, function);
                break;
            case NodeType.ConditionalExpression:
                HandleConditionalExpression(node, previousNode, function);
                break;
            case NodeType.ContinueStatement:
                HandleContinueStatement(node, previousNode, function);
                break;
            case NodeType.DebuggerStatement:
                HandleDebuggerStatement(node, previousNode, function);
                break;
            case NodeType.Decorator:
                HandleDecorator(node, previousNode, function);
                break;
            case NodeType.DoWhileStatement:
                HandleDoWhileStatement(node, previousNode, function);
                break;
            case NodeType.EmptyStatement:
                HandleEmptyStatement(node, previousNode, function);
                break;
            case NodeType.ExportAllDeclaration:
                HandleExportAllDeclaration(node, previousNode, function);
                break;
            case NodeType.ExportDefaultDeclaration:
                HandleExportDefaultDeclaration(node, previousNode, function);
                break;
            case NodeType.ExportNamedDeclaration:
                HandleExportNamedDeclaration(node, previousNode, function);
                break;
            case NodeType.ExportSpecifier:
                HandleExportSpecifier(node, previousNode, function);
                break;
            case NodeType.ExpressionStatement:
                HandleExpressionStatement(node, previousNode, function);
                break;
            case NodeType.ForInStatement:
                HandleForInStatement(node, previousNode, function);
                break;
            case NodeType.ForOfStatement:
                HandleForOfStatement(node, previousNode, function);
                break;
            case NodeType.ForStatement:
                HandleForStatement(node, previousNode, function);
                break;
            case NodeType.FunctionDeclaration:
                HandleFunctionDeclaration(node, previousNode, function);
                break;
            case NodeType.FunctionExpression:
                HandleFunctionExpression(node, previousNode, function);
                break;
            case NodeType.Identifier:
                HandleIdentifier(node, previousNode, function);
                break;
            case NodeType.IfStatement:
                HandleIfStatement(node, previousNode, function);
                break;
            case NodeType.ImportAttribute:
                HandleImportAttribute(node, previousNode, function);
                break;
            case NodeType.ImportDeclaration:
                HandleImportDeclaration(node, previousNode, function);
                break;
            case NodeType.ImportDefaultSpecifier:
                HandleImportDefaultSpecifier(node, previousNode, function);
                break;
            case NodeType.ImportExpression:
                HandleImportExpression(node, previousNode, function);
                break;
            case NodeType.ImportNamespaceSpecifier:
                HandleImportNamespaceSpecifier(node, previousNode, function);
                break;
            case NodeType.ImportSpecifier:
                HandleImportSpecifier(node, previousNode, function);
                break;
            case NodeType.LabeledStatement:
                HandleLabeledStatement(node, previousNode, function);
                break;
            case NodeType.Literal:
                HandleLiteral(node, previousNode, function);
                break;
            case NodeType.LogicalExpression:
                HandleLogicalExpression(node, previousNode, function);
                break;
            case NodeType.MemberExpression:
                HandleMemberExpression(node, previousNode, function);
                break;
            case NodeType.MetaProperty:
                HandleMetaProperty(node, previousNode, function);
                break;
            case NodeType.MethodDefinition:
                HandleMethodDefinition(node, previousNode, function);
                break;
            case NodeType.NewExpression:
                HandleNewExpression(node, previousNode, function);
                break;
            case NodeType.ObjectExpression:
                HandleObjectExpression(node, previousNode, function);
                break;
            case NodeType.ObjectPattern:
                HandleObjectPattern(node, previousNode, function);
                break;
            case NodeType.ParenthesizedExpression:
                HandleParenthesizedExpression(node, previousNode, function);
                break;
            case NodeType.PrivateIdentifier:
                HandlePrivateIdentifier(node, previousNode, function);
                break;
            case NodeType.Program:
                HandleProgram(node, previousNode, function);
                break;
            case NodeType.Property:
                HandleProperty(node, previousNode, function);
                break;
            case NodeType.PropertyDefinition:
                HandlePropertyDefinition(node, previousNode, function);
                break;
            case NodeType.RestElement:
                HandleRestElement(node, previousNode, function);
                break;
            case NodeType.ReturnStatement:
                HandleReturnStatement(node, previousNode, function);
                break;
            case NodeType.SequenceExpression:
                HandleSequenceExpression(node, previousNode, function);
                break;
            case NodeType.SpreadElement:
                HandleSpreadElement(node, previousNode, function);
                break;
            case NodeType.StaticBlock:
                HandleStaticBlock(node, previousNode, function);
                break;
            case NodeType.Super:
                HandleSuper(node, previousNode, function);
                break;
            case NodeType.SwitchCase:
                HandleSwitchCase(node, previousNode, function);
                break;
            case NodeType.SwitchStatement:
                HandleSwitchStatement(node, previousNode, function);
                break;
            case NodeType.TaggedTemplateExpression:
                HandleTaggedTemplateExpression(node, previousNode, function);
                break;
            case NodeType.TemplateElement:
                HandleTemplateElement(node, previousNode, function);
                break;
            case NodeType.TemplateLiteral:
                HandleTemplateLiteral(node, previousNode, function);
                break;
            case NodeType.ThisExpression:
                HandleThisExpression(node, previousNode, function);
                break;
            case NodeType.ThrowStatement:
                HandleThrowStatement(node, previousNode, function);
                break;
            case NodeType.TryStatement:
                HandleTryStatement(node, previousNode, function);
                break;
            case NodeType.UnaryExpression:
                HandleUnaryExpression(node, previousNode, function);
                break;
            case NodeType.UpdateExpression:
                HandleUpdateExpression(node, previousNode, function);
                break;
            case NodeType.VariableDeclaration:
                HandleVariableDeclaration(node, previousNode, function);
                break;
            case NodeType.VariableDeclarator:
                HandleVariableDeclarator(node, previousNode, function);
                break;
            case NodeType.WhileStatement:
                HandleWhileStatement(node, previousNode, function);
                break;
            case NodeType.WithStatement:
                HandleWithStatement(node, previousNode, function);
                break;
            case NodeType.YieldExpression:
                HandleYieldExpression(node, previousNode, function);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    // Note on this compiler flag. It will tell the compiler to inline this peace of code into the
    // NodeDelegationHandler. So in short this function would disappear in compilation step.
    // This mean we do not need to care about not used arguments we did not need that were autogenerated.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUnknownType(Node node, Node previousNode, Function function)
    {
        // this can happen if the parser gets behind the ECMAScript specifications
        // TODO: Add logging and warning upon parsing unknown specification
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleAccessorProperty(Node node, Node previousNode, Function function)
    {
        var accessorProperty = node.As<AccessorProperty>();
        AddSyntaxBeginOrEndTag(function, nameof(AccessorProperty), true);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, accessorProperty.Kind.ToString()));
        foreach (var decorator in accessorProperty.Decorators)
            NodeDelegationHandler(decorator, accessorProperty, function);
        if (accessorProperty.Value != null)
            NodeDelegationHandler(accessorProperty.Value, accessorProperty, function);
        AddSyntaxBeginOrEndTag(function, nameof(AccessorProperty), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleArrayExpression(Node node, Node previousNode, Function function)
    {
        var arrayExpression = node.As<ArrayExpression>();
        AddSyntaxBeginOrEndTag(function, nameof(ArrayExpression), true);
        foreach (var expression in arrayExpression.Elements.OfType<Expression>())
            NodeDelegationHandler(expression, arrayExpression, function);
        AddSyntaxBeginOrEndTag(function, nameof(ArrayExpression), false);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleArrayPattern(Node node, Node previousNode, Function function)
    {
        var arrayPattern = node.As<ArrayPattern>();
        AddSyntaxBeginOrEndTag(function, nameof(ArrayPattern), true);
        foreach (var pattern in arrayPattern.Elements.OfType<Node>())
        {
            NodeDelegationHandler(pattern, arrayPattern, function);
        }
        AddSyntaxBeginOrEndTag(function, nameof(ArrayPattern), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleArrowFunctionExpression(Node node, Node previousNode, Function function)
    {
        var arrowFunctionExpression = node.As<ArrowFunctionExpression>();
        AddSyntaxBeginOrEndTag(function,nameof(ArrowFunctionExpression), true);
        if (arrowFunctionExpression.Async)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Async, FunctionDataHelper.Async));
        var functionName = GetFunctionName(arrowFunctionExpression, previousNode);
        function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionName, functionName));
        function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionArgumentCount, arrowFunctionExpression.Params.Count.ToString()));
        foreach (var param in arrowFunctionExpression.Params)
            NodeDelegationHandler(param, arrowFunctionExpression, function);
        
        //we dont index test(function() {}) expressions
        if (previousNode.Type == NodeType.CallExpression)
        {
            NodeDelegationHandler(arrowFunctionExpression.Body, arrowFunctionExpression, function);
            AddSyntaxBeginOrEndTag(function, nameof(ArrowFunctionExpression), false);
            return;
        }
        AddSyntaxBeginOrEndTag(function, nameof(ArrowFunctionExpression), false);
        
        // make sure we start new process of function signature creation
        VisitNode(arrowFunctionExpression, previousNode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleAssignmentExpression(Node node, Node previousNode, Function function)
    {
        var assignmentExpression = node.As<AssignmentExpression>();
        AddSyntaxBeginOrEndTag(function,nameof(AssignmentExpression), true);
        NodeDelegationHandler(assignmentExpression.Left, assignmentExpression, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, assignmentExpression.Operator.ToString()));
        NodeDelegationHandler(assignmentExpression.Right, assignmentExpression, function);
        AddSyntaxBeginOrEndTag(function,nameof(AssignmentExpression), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleAssignmentPattern(Node node, Node previousNode, Function function)
    {
        var assignmentPattern = node.As<AssignmentPattern>();
        AddSyntaxBeginOrEndTag(function,nameof(AssignmentPattern), true);
        NodeDelegationHandler(assignmentPattern.Left, assignmentPattern, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, FunctionDataHelper.EqualsSymbol));
        NodeDelegationHandler(assignmentPattern.Right, assignmentPattern, function);
        AddSyntaxBeginOrEndTag(function,nameof(AssignmentPattern), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleAwaitExpression(Node node, Node previousNode, Function function)
    {
        var awaitExpression = node.As<AwaitExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Async, nameof(AwaitExpression)));
        AddSyntaxBeginOrEndTag(function,nameof(AwaitExpression), true);
        NodeDelegationHandler(awaitExpression.Argument, awaitExpression, function);
        AddSyntaxBeginOrEndTag(function,nameof(AwaitExpression), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleBinaryExpression(Node node, Node previousNode, Function function)
    {
        var binaryExpression = node.As<BinaryExpression>();
        AddSyntaxBeginOrEndTag(function,nameof(BinaryExpression), true);
        NodeDelegationHandler(binaryExpression.Left, binaryExpression, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, binaryExpression.Operator.ToString()));
        NodeDelegationHandler(binaryExpression.Right, binaryExpression, function);
        AddSyntaxBeginOrEndTag(function,nameof(BinaryExpression), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleBlockStatement(Node node, Node previousNode, Function function)
    {
        var blockStatement = node.As<BlockStatement>();
        AddSyntaxBeginOrEndTag(function,nameof(BlockStatement), true);
        foreach (var statement in blockStatement.Body.OfType<Statement>())
        {
            NodeDelegationHandler(statement, blockStatement, function);
        }
        AddSyntaxBeginOrEndTag(function,nameof(BlockStatement), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleBreakStatement(Node node, Node previousNode, Function function)
    {
        var breakStatement = node.As<BreakStatement>();
        AddControlFlowBeginOrEndTag(function, nameof(BreakStatement), true);
        if (breakStatement.Label != null)
            NodeDelegationHandler(breakStatement.Label, breakStatement, function);
        AddControlFlowBeginOrEndTag(function, nameof(BreakStatement), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleCallExpression(Node node, Node previousNode, Function function)
    {
        var callExpression = node.As<CallExpression>();
        AddSyntaxBeginOrEndTag(function, nameof(CallExpression), true);
        NodeDelegationHandler(callExpression.Callee, callExpression, function);
        if (callExpression.Optional)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, FunctionDataHelper.Optional));
        foreach (var argument in callExpression.Arguments.OfType<Expression>())
        {
            NodeDelegationHandler(argument, callExpression, function);
        }
        AddSyntaxBeginOrEndTag(function, nameof(CallExpression), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleCatchClause(Node node, Node previousNode, Function function)
    {
        var catchClause = node.As<CatchClause>();
        AddControlFlowBeginOrEndTag(function, nameof(CatchClause), true);
        if (catchClause.Param != null)
            NodeDelegationHandler(catchClause.Param, catchClause, function);
        NodeDelegationHandler(catchClause.Body, catchClause, function);
        AddControlFlowBeginOrEndTag(function, nameof(CatchClause), false);
    }

    private void HandleChainExpression(Node node, Node previousNode, Function function)
    {
        var chainExpression = node.As<ChainExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ChainExpression)));
        NodeDelegationHandler(chainExpression.Expression, chainExpression, function);
    }

    private void HandleClassBody(Node node, Node previousNode, Function function)
    {
        var classBody = node.As<ClassBody>();
        AddSyntaxBeginOrEndTag(function, nameof(ClassBody), true);
        
        foreach (var classElement in classBody.Body)
        {
            NodeDelegationHandler(classElement, classBody, function);
        }
        AddSyntaxBeginOrEndTag(function, nameof(ClassBody), false);
    }

    private void HandleClassDeclaration(Node node, Node previousNode, Function function)
    {
        var classDeclaration = node.As<ClassDeclaration>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Types, nameof(ClassDeclaration)));
        function.ExtractedFeatures.Add((ExtractedFeatureType.ClassDeclarationName, classDeclaration.Id?.Name ?? "")); ;
        foreach (var decorator in classDeclaration.Decorators)
        {
            NodeDelegationHandler(decorator, classDeclaration, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, FunctionDataHelper.BeginObject));
        NodeDelegationHandler(classDeclaration.Body, classDeclaration, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, FunctionDataHelper.EndObject));
    }

    private void HandleClassExpression(Node node, Node previousNode, Function function)
    {
        var classExpression = node.As<ClassExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Types, nameof(ClassExpression)));
        function.ExtractedFeatures.Add((ExtractedFeatureType.ClassDeclarationName, GetObjectName(classExpression, previousNode)));
        foreach (var decorator in classExpression.Decorators)
        {
            NodeDelegationHandler(decorator, classExpression, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, FunctionDataHelper.BeginObject));
        NodeDelegationHandler(classExpression.Body, classExpression, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, FunctionDataHelper.EndObject));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleConditionalExpression(Node node, Node previousNode, Function function)
    {
        var conditionalExpression = node.As<ConditionalExpression>();
        AddControlFlowBeginOrEndTag(function, nameof(ConditionalExpression), true);
        AddCodeStructure(function, nameof(ConditionalExpression.Test));
        NodeDelegationHandler(conditionalExpression.Test, conditionalExpression, function);
        AddCodeStructure(function, nameof(ConditionalExpression.Consequent));
        NodeDelegationHandler(conditionalExpression.Consequent, conditionalExpression, function);
        AddCodeStructure(function, nameof(ConditionalExpression.Alternate));
        NodeDelegationHandler(conditionalExpression.Alternate, conditionalExpression, function);
        AddControlFlowBeginOrEndTag(function, nameof(ConditionalExpression), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleContinueStatement(Node node, Node previousNode, Function function)
    {
        var continueStatement = node.As<ContinueStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(ContinueStatement)));
        if (continueStatement.Label != null)
            NodeDelegationHandler(continueStatement.Label, continueStatement, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleDebuggerStatement(Node node, Node previousNode, Function function)
    {
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(DebuggerStatement)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleDecorator(Node node, Node previousNode, Function function)
    {
        var decorator = node.As<Decorator>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(Decorator)));
        NodeDelegationHandler(decorator.Expression, decorator, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleDoWhileStatement(Node node, Node previousNode, Function function)
    {
        var doWhileStatement = node.As<DoWhileStatement>();
        AddControlFlowBeginOrEndTag(function, nameof(DoWhileStatement), true);
        AddCodeStructure(function, nameof(DoWhileStatement.Test));
        NodeDelegationHandler(doWhileStatement.Test, doWhileStatement, function);
        AddCodeStructure(function, nameof(DoWhileStatement.Body));
        NodeDelegationHandler(doWhileStatement.Body, doWhileStatement, function);
        AddControlFlowBeginOrEndTag(function, nameof(DoWhileStatement), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleEmptyStatement(Node node, Node previousNode, Function function)
    {
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(EmptyStatement)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleExportAllDeclaration(Node node, Node previousNode, Function function)
    {
        var exportAllDeclaration = node.As<ExportAllDeclaration>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ExportAllDeclaration)));
        if (exportAllDeclaration.Exported != null)
            NodeDelegationHandler(exportAllDeclaration.Exported, exportAllDeclaration, function);
        NodeDelegationHandler(exportAllDeclaration.Source, exportAllDeclaration, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleExportDefaultDeclaration(Node node, Node previousNode, Function function)
    {
        var exportDefaultDeclaration = node.As<ExportDefaultDeclaration>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ExportDefaultDeclaration)));
        NodeDelegationHandler(exportDefaultDeclaration.Declaration, exportDefaultDeclaration, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleExportNamedDeclaration(Node node, Node previousNode, Function function)
    {
        var exportNameDeclaration = node.As<ExportNamedDeclaration>();
        if (exportNameDeclaration.Source != null)
            NodeDelegationHandler(exportNameDeclaration.Source, exportNameDeclaration, function);
        if (exportNameDeclaration.Declaration != null)
            NodeDelegationHandler(exportNameDeclaration.Declaration, exportNameDeclaration, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleExportSpecifier(Node node, Node previousNode, Function function)
    {
        var handleExportSpecifier = node.As<ExportSpecifier>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ExportSpecifier)));
        NodeDelegationHandler(handleExportSpecifier.Exported, handleExportSpecifier, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleExpressionStatement(Node node, Node previousNode, Function function)
    {
        var expressionStatement = node.As<ExpressionStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ExpressionStatement)));
        NodeDelegationHandler(expressionStatement.Expression, expressionStatement, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleForInStatement(Node node, Node previousNode, Function function)
    {
        var forInStatement = node.As<ForInStatement>();
        AddControlFlowBeginOrEndTag(function, nameof(ForInStatement), true);
        AddCodeStructure(function, nameof(ForInStatement.Left));
        NodeDelegationHandler(forInStatement.Left, forInStatement, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, FunctionDataHelper.In));
        AddCodeStructure(function, nameof(ForInStatement.Right));
        NodeDelegationHandler(forInStatement.Right, forInStatement, function);
        AddCodeStructure(function, nameof(ForInStatement.Body));
        NodeDelegationHandler(forInStatement.Body, forInStatement, function);
        AddControlFlowBeginOrEndTag(function, nameof(ForInStatement), false);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleForOfStatement(Node node, Node previousNode, Function function)
    {
        var forOfStatement = node.As<ForOfStatement>();
        if (forOfStatement.Await)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Async, FunctionDataHelper.Await));
        AddControlFlowBeginOrEndTag(function, nameof(ForOfStatement), true);
        AddCodeStructure(function, nameof(ForOfStatement.Left));
        NodeDelegationHandler(forOfStatement.Left, forOfStatement, function);
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, FunctionDataHelper.Of));
        AddCodeStructure(function, nameof(ForOfStatement.Right));
        NodeDelegationHandler(forOfStatement.Right, forOfStatement, function);
        AddCodeStructure(function, nameof(ForOfStatement.Body));
        NodeDelegationHandler(forOfStatement.Body, forOfStatement, function);
        AddControlFlowBeginOrEndTag(function, nameof(ForOfStatement), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleForStatement(Node node, Node previousNode, Function function)
    {
        var forStatement = node.As<ForStatement>();
        AddControlFlowBeginOrEndTag(function,nameof(HandleForStatement), true);
        if (forStatement.Init != null)
        {
            AddCodeStructure(function, nameof(ForStatement.Init));
            NodeDelegationHandler(forStatement.Init, forStatement, function);
        }

        if (forStatement.Test != null)
        {
            AddCodeStructure(function, nameof(ForStatement.Test));
            NodeDelegationHandler(forStatement.Test, forStatement, function);
        }

        if (forStatement.Update != null)
        {
            AddCodeStructure(function, nameof(ForStatement.Update));
            NodeDelegationHandler(forStatement.Update, forStatement, function);
        }
        
        AddCodeStructure(function, nameof(ForStatement.Body));
        NodeDelegationHandler(forStatement.Body, forStatement, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleFunctionDeclaration(Node node, Node previousNode, Function function)
    {
        var functionDeclaration = node.As<FunctionDeclaration>();
        if (functionDeclaration.Async)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Async, FunctionDataHelper.Async));
        AddSyntaxBeginOrEndTag(function,nameof(FunctionDeclaration), true);
        function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionName, functionDeclaration.Id?.Name ?? FunctionDataHelper.Anonymous));
        function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionArgumentCount, functionDeclaration.Params.Count.ToString()));
        foreach (var param in functionDeclaration.Params)
            NodeDelegationHandler(param, functionDeclaration, function);

        //we dont index test(function() {}) expressions
        if (previousNode.Type == NodeType.CallExpression)
        {
            NodeDelegationHandler(functionDeclaration.Body, functionDeclaration, function);
            AddSyntaxBeginOrEndTag(function,nameof(FunctionDeclaration), false);
            return;
        }
        AddSyntaxBeginOrEndTag(function,nameof(FunctionDeclaration), false);
        
        // make sure we start new process of function signature creation
        VisitNode(functionDeclaration, previousNode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleFunctionExpression(Node node, Node previousNode, Function function)
    {
        var functionExpression = node.As<FunctionExpression>();
        AddSyntaxBeginOrEndTag(function,nameof(FunctionExpression), true);
        if (functionExpression.Async)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Async, FunctionDataHelper.Async));
        var functionName = GetFunctionName(functionExpression, previousNode);
        function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionName, functionName));
        function.ExtractedFeatures.Add((ExtractedFeatureType.FunctionArgumentCount, functionExpression.Params.Count.ToString()));
        foreach (var param in functionExpression.Params)
            NodeDelegationHandler(param, functionExpression, function);
        
        //we dont index test(function() {}) expressions
        if (previousNode.Type == NodeType.CallExpression)
        {
            NodeDelegationHandler(functionExpression.Body, functionExpression, function);
            AddSyntaxBeginOrEndTag(function,nameof(FunctionExpression), false);
            return;
        }
        AddSyntaxBeginOrEndTag(function,nameof(FunctionExpression), false);
        
        // make sure we start new process of function signature creation
        VisitNode(functionExpression, previousNode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleIdentifier(Node node, Node? previousNode, Function function)
    {
        var identifier = node.As<Identifier>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(Identifier)));
        function.ExtractedFeatures.Add((ExtractedFeatureType.VariableName, identifier.Name));
        
        if (previousNode?.Type == NodeType.VariableDeclarator) return;
        
        if (JavaScriptHelper.AsyncIdentifiers.Contains(identifier.Name))
        {
            function.ExtractedFeatures.Add((ExtractedFeatureType.Async, identifier.Name));
            return;
        }
        if (JavaScriptHelper.HostEnvironmentObjects.Contains(identifier.Name))
        {
            function.ExtractedFeatures.Add((ExtractedFeatureType.HostEnvironmentObject, identifier.Name));
            return;
        }
        if (JavaScriptHelper.ECMAScriptObjects.Contains(identifier.Name))
        {
            function.ExtractedFeatures.Add((ExtractedFeatureType.ECMAObject, identifier.Name));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleIfStatement(Node node, Node previousNode, Function function)
    {
        var ifStatement = node.As<IfStatement>();
        AddControlFlowBeginOrEndTag(function, nameof(IfStatement), true);
        AddCodeStructure(function, nameof(IfStatement.Test));
        NodeDelegationHandler(ifStatement.Test, ifStatement, function);
        AddCodeStructure(function, nameof(IfStatement.Consequent));
        NodeDelegationHandler(ifStatement.Consequent, ifStatement, function);
        if (ifStatement.Alternate != null)
        {
            AddCodeStructure(function, nameof(IfStatement.Alternate));
            NodeDelegationHandler(ifStatement.Alternate, ifStatement, function);
        }

        AddControlFlowBeginOrEndTag(function, nameof(IfStatement), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleImportAttribute(Node node, Node previousNode, Function function)
    {
        var importAttribute = node.As<ImportAttribute>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ImportAttribute)));
        NodeDelegationHandler(importAttribute.Key, importAttribute, function);
        NodeDelegationHandler(importAttribute.Value, importAttribute, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleImportDeclaration(Node node, Node previousNode, Function function)
    {
        var importDeclaration = node.As<ImportDeclaration>();
        AddSyntaxBeginOrEndTag(function, nameof(ImportDeclaration), true);
        NodeDelegationHandler(importDeclaration.Source, importDeclaration, function);
        foreach (var attribute in importDeclaration.Attributes)
        {
            NodeDelegationHandler(attribute, importDeclaration, function);
        }

        foreach (var specifier in importDeclaration.Specifiers)
        {
            NodeDelegationHandler(specifier, importDeclaration, function);
        }
        
        AddSyntaxBeginOrEndTag(function, nameof(ImportDeclaration), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleImportDefaultSpecifier(Node node, Node previousNode, Function function)
    {
        var importSpecifier = node.As<ImportSpecifier>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ImportSpecifier)));
        NodeDelegationHandler(importSpecifier.Imported, importSpecifier, function);
        NodeDelegationHandler(importSpecifier.Local, importSpecifier, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleImportExpression(Node node, Node previousNode, Function function)
    {
        var importExpression = node.As<ImportExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ImportExpression)));
        NodeDelegationHandler(importExpression.Source, importExpression, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleImportNamespaceSpecifier(Node node, Node previousNode, Function function)
    {
        var importExpression = node.As<ImportNamespaceSpecifier>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ImportNamespaceSpecifier)));
        NodeDelegationHandler(importExpression.Local, importExpression, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleImportSpecifier(Node node, Node previousNode, Function function)
    {
        var importStatement = node.As<ImportSpecifier>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ImportSpecifier)));
        NodeDelegationHandler(importStatement.Imported, importStatement, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleLabeledStatement(Node node, Node previousNode, Function function)
    {
        var labelStatement = node.As<LabeledStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(LabeledStatement)));
        NodeDelegationHandler(labelStatement.Label, labelStatement, function);
        NodeDelegationHandler(labelStatement.Body, labelStatement, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleLiteral(Node node, Node previousNode, Function function)
    {
        var literal = node.As<Literal>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Types, literal.Kind.ToString()));
        function.ExtractedFeatures.Add(literal.Kind == TokenKind.StringLiteral
            ? (ExtractedFeatureType.Strings, literal.Raw)
            : (ExtractedFeatureType.Literals, literal.Raw));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleLogicalExpression(Node node, Node previousNode, Function function)
    {
        var logicalExpression = node.As<LogicalExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, logicalExpression.Operator.ToString()));
        NodeDelegationHandler(logicalExpression.Left, logicalExpression, function);
        NodeDelegationHandler(logicalExpression.Right, logicalExpression, function);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleMemberExpression(Node node, Node previousNode, Function function)
    {
        var memberExpression = node.As<MemberExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(MemberExpression)));
        NodeDelegationHandler(memberExpression.Object, memberExpression, function);
        if (memberExpression.Computed)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, FunctionDataHelper.Computed));
        NodeDelegationHandler(memberExpression.Property,memberExpression, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleMetaProperty(Node node, Node previousNode, Function function)
    {
        var metaProperty = node.As<MetaProperty>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(MetaProperty)));
        NodeDelegationHandler(metaProperty.Meta, previousNode, function);
        NodeDelegationHandler(metaProperty.Property, previousNode, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleMethodDefinition(Node node, Node previousNode, Function function)
    {
        var methodDefinition = node.As<MethodDefinition>();
        AddSyntaxBeginOrEndTag(function, nameof(MethodDefinition), true);
        AddCodeStructure(function, nameof(MethodDefinition.Decorators));
        foreach (var decorator in methodDefinition.Decorators)
        {
            NodeDelegationHandler(decorator, methodDefinition, function);
        }
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, methodDefinition.Kind.ToString()));
        NodeDelegationHandler(methodDefinition.Key, methodDefinition, function);
        NodeDelegationHandler(methodDefinition.Value, methodDefinition, function);
        AddSyntaxBeginOrEndTag(function, nameof(MethodDefinition), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleNewExpression(Node node, Node previousNode, Function function)
    {
        var newExpression = node.As<NewExpression>();
        AddSyntaxBeginOrEndTag(function, nameof(NewExpression), true);
        NodeDelegationHandler(newExpression.Callee, newExpression, function);
        foreach (var argument in newExpression.Arguments.OfType<Expression>())
            NodeDelegationHandler(argument, newExpression, function);
        AddSyntaxBeginOrEndTag(function, nameof(NewExpression), false);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleObjectExpression(Node node, Node previousNode, Function function)
    {
        var objectExpression = node.As<ObjectExpression>();
        if (objectExpression.ChildNodes.IsEmpty())
        {
            function.ExtractedFeatures.Add((ExtractedFeatureType.Types, nameof(ObjectExpression)));
            return;
        }

        AddSyntaxBeginOrEndTag(function, nameof(ObjectExpression), true);
        foreach (var childNode in objectExpression.ChildNodes)
        {
            NodeDelegationHandler(childNode,  objectExpression, function);
        }
        AddSyntaxBeginOrEndTag(function, nameof(ObjectExpression), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleObjectPattern(Node node, Node previousNode, Function function)
    {
        var objectPattern = node.As<ObjectPattern>();
        AddSyntaxBeginOrEndTag(function, nameof(ObjectPattern), true);
        foreach (var property in objectPattern.Properties)
        {
            NodeDelegationHandler(property, objectPattern, function);
        }
        AddSyntaxBeginOrEndTag(function, nameof(ObjectPattern), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleParenthesizedExpression(Node node, Node previousNode, Function function)
    {
        var parenthesizedExpression = node.As<ParenthesizedExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ParenthesizedExpression)));
        NodeDelegationHandler(parenthesizedExpression.Expression, previousNode, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandlePrivateIdentifier(Node node, Node previousNode, Function function)
    {
        var privateIdentifier = node.As<PrivateIdentifier>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(PrivateIdentifier)));
        function.ExtractedFeatures.Add((ExtractedFeatureType.VariableName, privateIdentifier.Name));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleProgram(Node node, Node previousNode, Function function)
    {
        // This cannot happen because the root is handled by the VisitNode function
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleProperty(Node node, Node previousNode, Function function)
    {
        var property = node.As<Property>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(Property)));
        function.ExtractedFeatures.Add((ExtractedFeatureType.Types, property.Kind.ToString()));
        NodeDelegationHandler(property.Key, property, function);
        NodeDelegationHandler(property.Value, property, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandlePropertyDefinition(Node node, Node previousNode, Function function)
    {
        var propertyDefinition = node.As<PropertyDefinition>();
        AddSyntaxBeginOrEndTag(function, nameof(PropertyDefinition), true);
        foreach (var decorator in propertyDefinition.Decorators)
            NodeDelegationHandler(decorator, propertyDefinition, function);
        if (propertyDefinition.Value != null) 
            NodeDelegationHandler(propertyDefinition.Value, previousNode, function);
        AddSyntaxBeginOrEndTag(function, nameof(PropertyDefinition), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleRestElement(Node node, Node previousNode, Function function)
    {
        var restElement = node.As<RestElement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(RestElement)));
        NodeDelegationHandler(restElement.Argument, restElement, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleReturnStatement(Node node, Node previousNode, Function function)
    {
        var returnStatement = node.As<ReturnStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow,nameof(ReturnStatement)));
        if (returnStatement.Argument != null)
            NodeDelegationHandler(returnStatement.Argument, returnStatement, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleSequenceExpression(Node node, Node previousNode, Function function)
    {
        var sequenceExpression = node.As<SequenceExpression>();
        AddSyntaxBeginOrEndTag(function, nameof(SequenceExpression), true);
        foreach (var expression in sequenceExpression.Expressions)
        {
            NodeDelegationHandler(expression, sequenceExpression, function);
        }
        AddSyntaxBeginOrEndTag(function, nameof(SequenceExpression), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleSpreadElement(Node node, Node previousNode, Function function)
    {
        var spreadElement = node.As<SpreadElement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(SpreadElement)));
        NodeDelegationHandler(spreadElement.Argument, spreadElement, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleStaticBlock(Node node, Node previousNode, Function function)
    {
        var staticBlock = node.As<StaticBlock>();
        AddSyntaxBeginOrEndTag(function, nameof(StaticBlock), true);
        foreach (var statement in staticBlock.Body)
        {
            NodeDelegationHandler(statement, staticBlock, function);
        }
        AddSyntaxBeginOrEndTag(function, nameof(StaticBlock), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleSuper(Node node, Node previousNode, Function function)
    {
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(Super)));
    }

    private void HandleSwitchCase(Node node, Node previousNode, Function function)
    {
        var switchCase = node.As<SwitchCase>();
        AddControlFlowBeginOrEndTag(function, nameof(SwitchCase), true);
        if (switchCase.Test != null)
        {
            AddCodeStructure(function, nameof(SwitchCase.Test));
            NodeDelegationHandler(switchCase.Test, switchCase, function);
        }
        if (switchCase.Consequent.Count > 0) 
            AddCodeStructure(function, nameof(SwitchCase.Consequent));
        foreach (var consequent in switchCase.Consequent.OfType<Statement>())
        {
            NodeDelegationHandler(consequent, switchCase, function);
        }
        AddControlFlowBeginOrEndTag(function, nameof(SwitchCase), false);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleSwitchStatement(Node node, Node previousNode, Function function)
    {
        var switchStatement = node.As<SwitchStatement>();
        AddControlFlowBeginOrEndTag(function, nameof(SwitchStatement), true);
        AddCodeStructure(function, nameof(SwitchStatement.Discriminant));
        NodeDelegationHandler(switchStatement.Discriminant, switchStatement, function);
        AddCodeStructure(function, nameof(SwitchStatement.Cases));
        foreach (var switchCase in switchStatement.Cases)
        {
            HandleSwitchCase(switchCase, switchStatement, function);
        }
        AddControlFlowBeginOrEndTag(function, nameof(SwitchStatement), false);
    }

    private void HandleTaggedTemplateExpression(Node node, Node previousNode, Function function)
    {
        var taggedTemplateExpression = node.As<TaggedTemplateExpression>();
        AddSyntaxBeginOrEndTag(function, nameof(TaggedTemplateExpression), true);
        AddCodeStructure(function, nameof(TaggedTemplateExpression.Tag));
        NodeDelegationHandler(taggedTemplateExpression.Tag, previousNode, function);
        AddCodeStructure(function, nameof(TaggedTemplateExpression.Quasi));
        NodeDelegationHandler(taggedTemplateExpression.Quasi, previousNode, function);
        AddSyntaxBeginOrEndTag(function, nameof(TaggedTemplateExpression), false);
    }

    private void HandleTemplateElement(Node node, Node previousNode, Function function)
    {
        var templateElement = node.As<TemplateElement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Strings, templateElement.Value.Raw));
    }

    private void HandleTemplateLiteral(Node node, Node previousNode, Function function)
    {
        var templateLiteral = node.As<TemplateLiteral>();
        AddSyntaxBeginOrEndTag(function, nameof(TemplateLiteral), true);
        if (templateLiteral.Quasis.Count > 0)
            AddCodeStructure(function, nameof(TemplateLiteral.Quasis));
        foreach (var quasi in templateLiteral.Quasis)
        {
            function.ExtractedFeatures.Add((ExtractedFeatureType.Strings, quasi.Value.Raw));
        }

        if (templateLiteral.Expressions.Count > 0)
            AddCodeStructure(function, nameof(TemplateLiteral.Expressions));
        foreach (var expression in templateLiteral.Expressions)
        {
            NodeDelegationHandler(expression, templateLiteral, function);
        }
        AddSyntaxBeginOrEndTag(function, nameof(TemplateLiteral), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleThisExpression(Node node, Node previousNode, Function function)
    {
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(ThisExpression)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleThrowStatement(Node node, Node previousNode, Function function)
    {
        var throwStatement = node.As<ThrowStatement>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.ControlFlow, nameof(ThrowStatement)));
        NodeDelegationHandler(throwStatement.Argument, throwStatement, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleTryStatement(Node node, Node previousNode, Function function)
    {
        var tryStatement = node.As<TryStatement>();
        AddControlFlowBeginOrEndTag(function, nameof(TryStatement), true);
        AddCodeStructure(function, nameof(TryStatement.Block));
        NodeDelegationHandler(tryStatement.Block, tryStatement, function);
        if (tryStatement.Handler != null)
        {
            AddCodeStructure(function, nameof(TryStatement.Handler));
            NodeDelegationHandler(tryStatement.Handler, tryStatement, function);
        }

        if (tryStatement.Finalizer != null)
        {
            AddCodeStructure(function, nameof(tryStatement.Finalizer));
            NodeDelegationHandler(tryStatement.Finalizer, tryStatement, function);
        }

        AddControlFlowBeginOrEndTag(function, nameof(TryStatement), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUnaryExpression(Node node, Node previousNode, Function function)
    {
        var unaryExpression = node.As<UnaryExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(UnaryExpression)));
        NodeDelegationHandler(unaryExpression.Argument, unaryExpression, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleUpdateExpression(Node node, Node previousNode, Function function)
    {
        var updateExpression = node.As<UpdateExpression>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(UpdateExpression)));
        if (updateExpression.Prefix)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, updateExpression.Operator.ToString()));
        NodeDelegationHandler(updateExpression.Argument, updateExpression, function);
        if (!updateExpression.Prefix)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, updateExpression.Operator.ToString()));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleVariableDeclaration(Node node, Node previousNode, Function function)
    {
        var variableDeclaration = node.As<VariableDeclaration>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(VariableDeclaration)));
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, variableDeclaration.Kind.ToString()));
        foreach (var declaration in variableDeclaration.Declarations.OfType<VariableDeclarator>())
        {
            NodeDelegationHandler(declaration, variableDeclaration, function);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleVariableDeclarator(Node node, Node previousNode, Function function)
    {
        var variableDeclarator = node.As<VariableDeclarator>();
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(VariableDeclarator)));
        NodeDelegationHandler(variableDeclarator.Id, variableDeclarator, function);
        if (variableDeclarator.Init != null)
            NodeDelegationHandler(variableDeclarator.Init, variableDeclarator, function);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleWhileStatement(Node node, Node previousNode, Function function)
    {
        var whileStatement = node.As<WhileStatement>();
        AddControlFlowBeginOrEndTag(function, nameof(WhileStatement), true);
        AddCodeStructure(function, nameof(WhileStatement.Test));
        NodeDelegationHandler(whileStatement.Test, previousNode, function);
        AddCodeStructure(function, nameof(WhileStatement.Body));
        NodeDelegationHandler(whileStatement.Body, previousNode, function);
        AddControlFlowBeginOrEndTag(function, nameof(WhileStatement), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleWithStatement(Node node, Node previousNode, Function function)
    {
        var withStatement = node.As<WithStatement>();
        AddControlFlowBeginOrEndTag(function, nameof(WithStatement), true);
        AddCodeStructure(function, nameof(WithStatement.Object));
        NodeDelegationHandler(withStatement.Object, withStatement, function);
        AddCodeStructure(function, nameof(WithStatement.Body));
        NodeDelegationHandler(withStatement.Body, withStatement, function);
        AddControlFlowBeginOrEndTag(function, nameof(WithStatement), false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleYieldExpression(Node node, Node previousNode, Function function)
    {
        var yieldExpression = node.As<YieldExpression>();
        if (yieldExpression.Delegate)
            function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, FunctionDataHelper.Delegate));
        function.ExtractedFeatures.Add((ExtractedFeatureType.Syntax, nameof(YieldExpression)));
        if (yieldExpression.Argument != null)
            NodeDelegationHandler(yieldExpression.Argument, yieldExpression, function);
    }

    private string GetFunctionName(Node node, Node? previousNode)
    {
        if (node.Type == NodeType.FunctionExpression)
        {
            if (node.As<FunctionExpression>().Id != null)
                return node.As<FunctionExpression>().Id!.Name;
        }

        if (previousNode?.Type == NodeType.VariableDeclarator)
        {
            var variableDeclarator = previousNode.As<VariableDeclarator>();
            if (variableDeclarator.Id.Type == NodeType.Identifier)
            {
                return variableDeclarator.Id.As<Identifier>().Name;
            }

            return FunctionDataHelper.UnknownFunctionName;
        }

        if (previousNode?.Type == NodeType.ConditionalExpression)
        {
            return FunctionDataHelper.ConditionalAssignedFunction;
        }

        return FunctionDataHelper.Anonymous;
    }
    
    private string GetObjectName(ClassExpression classExpression, Node previousNode)
    {
        if (classExpression.Type == NodeType.ClassExpression)
        {
            if (classExpression.As<ClassExpression>().Id != null)
                return classExpression.As<ClassExpression>().Id!.Name;
        }

        if (previousNode.Type == NodeType.VariableDeclarator)
        {
            var variableDeclarator = previousNode.As<VariableDeclarator>();
            if (variableDeclarator.Id.Type == NodeType.Identifier)
            {
                return variableDeclarator.Id.As<Identifier>().Name;
            }

            return FunctionDataHelper.UnknownFunctionName;
        }
        
        if (previousNode?.Type == NodeType.ConditionalExpression)
        {
            return FunctionDataHelper.ConditionalAssignedClass;
        }

        return FunctionDataHelper.Anonymous;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddSyntaxBeginOrEndTag(Function function, string nameNode, bool isBegin)
    {
        function.ExtractedFeatures.Add(isBegin
            ? (ExtractedFeatureType.Syntax, nameNode + "-Begin")
            : (ExtractedFeatureType.Syntax, nameNode + "-End"));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddControlFlowBeginOrEndTag(Function function, string nameNode, bool isBegin)
    {
        function.ExtractedFeatures.Add(isBegin
            ? (ExtractedFeatureType.ControlFlow, nameNode + "-Begin")
            : (ExtractedFeatureType.ControlFlow, nameNode + "-End"));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddCodeStructure(Function function, string type)
    {
        function.ExtractedFeatures.Add((ExtractedFeatureType.CodeStructure, type));
    }
}

// Some special strings to make it easier changing things up.
public static class FunctionDataHelper
{
    public static readonly string Delegate = "Delegate";
    public static readonly string EqualsSymbol = "=";
    public static readonly string Optional = "Optional";
    public static readonly string In = "In";
    public static readonly string Of = "Of";
    public static readonly string Await = "Await";
    public static readonly string Anonymous = "Anonymous";
    public static readonly string Async = "Async";
    public static readonly string UnknownFunctionName = "Unknown";
    public static readonly string BeginObject = "BeginObject";
    public static readonly string EndObject = "EndObject";
    public static readonly string Computed = "Computed";
    public static readonly string ConditionalAssignedFunction = "ConditionalAssignedFunction";
    public static readonly string ConditionalAssignedClass = "ConditionalAssignedFunction";
    public static readonly string FunctionAsync = "FunctionAsync";
}
