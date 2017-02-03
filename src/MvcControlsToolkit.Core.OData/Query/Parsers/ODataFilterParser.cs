﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.UriParser;
using MvcControlsToolkit.Core.Views;

namespace MvcControlsToolkit.Core.OData.Parsers
{
    public class ODataFilterParser: ODataParserBase
    {
        private FilterClause filter;

        public ODataFilterParser(FilterClause x)
        {
            filter = x;
        }
        public QueryFilterBooleanOperator Parse()
        {

            var res = ParseRec(filter.Expression);
            if (res == null) return null;
            if (!(res is QueryFilterBooleanOperator))
                return new QueryFilterBooleanOperator(res, null);
            return res as QueryFilterBooleanOperator;
        }
        private QueryFilterClause ParseRec(Microsoft.OData.UriParser.QueryNode node)
        {
            if (node.Kind == QueryNodeKind.BinaryOperator)
            {
                var binaryOperator = node as BinaryOperatorNode;
                switch (binaryOperator.OperatorKind)
                {
                    case BinaryOperatorKind.And:
                        return new QueryFilterBooleanOperator(ParseRec(binaryOperator.Left), ParseRec(binaryOperator.Right))
                        { Operator = QueryFilterBooleanOperator.and }; 
                    case BinaryOperatorKind.Or:
                        return new QueryFilterBooleanOperator(ParseRec(binaryOperator.Left), ParseRec(binaryOperator.Right))
                        { Operator = QueryFilterBooleanOperator.or };
                    case BinaryOperatorKind.Equal:
                        return BuildComparison(binaryOperator.Left, binaryOperator.Right, "eq", "ne");
                    case BinaryOperatorKind.NotEqual:
                        return BuildComparison(binaryOperator.Left, binaryOperator.Right, "ne", "eq");
                    case BinaryOperatorKind.GreaterThan:
                        return BuildComparison(binaryOperator.Left, binaryOperator.Right, "gt", "le");
                    case BinaryOperatorKind.LessThanOrEqual:
                        return BuildComparison(binaryOperator.Left, binaryOperator.Right, "le", "gt");
                    case BinaryOperatorKind.LessThan:
                        return BuildComparison(binaryOperator.Left, binaryOperator.Right, "lt", "ge");
                    case BinaryOperatorKind.GreaterThanOrEqual:
                        return BuildComparison(binaryOperator.Left, binaryOperator.Right, "ge", "lt");
                    default:
                        return null;


                }


            }
            else if(node.Kind == QueryNodeKind.UnaryOperator)
            {
                var unaryOperator = node as UnaryOperatorNode;
                if (unaryOperator.OperatorKind == UnaryOperatorKind.Not)
                    return new QueryFilterBooleanOperator(ParseRec(unaryOperator.Operand), null)
                    { Operator = QueryFilterBooleanOperator.not };
                else return null;
            }
            else if (node.Kind == QueryNodeKind.SingleValueFunctionCall)
            {
                var functionCall = node as SingleValueFunctionCallNode;
                if (functionCall.Name == null || functionCall.Parameters == null) return null;
                var args = functionCall.Parameters.ToList();
                if (args.Count != 2) return null;
                switch (functionCall.Name.ToLower())
                {
                    case "contains":
                        return BuildComparison(args[0], args[1], "contains", null);
                    case "startswith":
                        return BuildComparison(args[0], args[1], "startswith", null);
                    case "endswith":
                        return BuildComparison(args[0], args[1], "endswith", null);
                    default:
                        return null;
                }
            }
            else return null;
        } 
        private object convertValue(object value, out short dateTimeType, Type propertyType)
        {
            dateTimeType = 0;
            propertyType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (value == null) return null;
            if (value is Microsoft.OData.Edm.Date) 
            {
                Microsoft.OData.Edm.Date dt = (Microsoft.OData.Edm.Date)value;
                value = new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Unspecified);
                dateTimeType = QueryFilterCondition.IsDate;


            }
            else if (value is  Microsoft.OData.Edm.TimeOfDay)
            {
                value = (TimeSpan)value;
                dateTimeType = QueryFilterCondition.IsTime;
            }
            else if (value is DateTimeOffset)
            {
                dateTimeType = QueryFilterCondition.IsDateTime;
                if(propertyType == typeof(DateTime))
                {
                    var cvalue = ((DateTimeOffset)value).UtcDateTime;
                    value = new DateTime(cvalue.Year, cvalue.Month, cvalue.Day,
                        cvalue.Hour, cvalue.Minute, cvalue.Second, cvalue.Millisecond, DateTimeKind.Unspecified);
                }
                    
            }
            else if (value is TimeSpan)
            {
                dateTimeType = QueryFilterCondition.IsDuration;
            }
            return value;
        }
        private QueryFilterCondition BuildComparison(Microsoft.OData.UriParser.QueryNode left, Microsoft.OData.UriParser.QueryNode right, string normalOperator, string inverseOperator)
        {
            bool inv = false;
            string propertyName = null;
            Type propertyType;
            object value = null;
            short dateTimeType;
            if (left.Kind == QueryNodeKind.Constant)
            {
                if (right.Kind == QueryNodeKind.SingleValuePropertyAccess)
                {
                    var cnode = right as SingleValuePropertyAccessNode;
                    propertyName = buildPropertyAccess(cnode);
                    propertyType = (cnode.Property as EdmClrProperty).Property.PropertyType;
                }
                else return null;
                inv = true;
                value = convertValue((left as ConstantNode).Value, out dateTimeType, propertyType);
                if (value == null) return null;
                
            }
            else if (right.Kind == QueryNodeKind.Constant)
            {
                if (left.Kind == QueryNodeKind.SingleValuePropertyAccess)
                {
                    var cnode = left as SingleValuePropertyAccessNode;
                    propertyType = (cnode.Property as EdmClrProperty).Property.PropertyType;
                    propertyName = buildPropertyAccess(cnode);
                }
                else return null;
                value = convertValue((right as ConstantNode).Value, out dateTimeType, propertyType);
                if (value == null) return null;
            }
            else return null;
            if (propertyName == null) return null;
            return new QueryFilterCondition()
            {
                Operator= inv && inverseOperator != null ? inverseOperator : normalOperator,
                Property=propertyName,
                Value=value,
                DateTimeType=dateTimeType,
                Inv = inv && inverseOperator == null
            };
        }
        
        
    }
}
