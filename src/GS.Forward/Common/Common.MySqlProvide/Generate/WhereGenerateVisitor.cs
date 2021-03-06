﻿using Common.Core.CusStruct;
using Common.MySqlProvide.CusAttr;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Common.MySqlProvide.Generate
{

    /// <summary>
    /// @auth : monster
    /// @since : 5/20/2020 4:28:15 PM
    /// @source : 
    /// @des : 构建WHERE
    /// </summary>
    [Obsolete("存在sql注入风险")]
    public partial class WhereGenerateVisitor : ExpressionVisitor
    {

        protected override Expression VisitUnary(UnaryExpression u)
        {
            SetOptType(u.NodeType);
            return u;
        }

        protected override Expression VisitBinary(BinaryExpression expression)
        {

            this.Visit(expression.Left);

            SetOptType(expression.NodeType);

            this.Visit(expression.Right);
            return expression;
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            if (c.Value == null)
            {
                this.Append(null);
            }
            else
            {
                this.Append(c.Value);
            }
            return c;
        }

        protected override Expression VisitMember(MemberExpression m)
        {
            if (m.Expression != null)
            {
                if (m.Expression.NodeType == ExpressionType.Parameter)
                {
                    AliasAttribute alias = m.Member.GetCustomAttribute<AliasAttribute>();

                    if (alias != null)
                        this.Append(alias.Name);
                    else
                        this.Append(m.Member.Name);
                    return m;
                }
                else if (m.Expression.NodeType == ExpressionType.Constant)
                {// 获取局部变量
                    var @object = ((ConstantExpression)m.Expression).Value; //这个是重点

                    if (m.Member.MemberType == MemberTypes.Field)
                    {
                        var value = ((FieldInfo)m.Member).GetValue(@object);
                        this.Append(value);
                        return m;
                    }
                    else if (m.Member.MemberType == MemberTypes.Property)
                    {
                        var value = ((PropertyInfo)m.Member).GetValue(@object);
                        this.Append(value);
                        return m;
                    }
                }
                else if (m.Expression.NodeType == ExpressionType.MemberAccess)
                {// TODO 获取对象属性值

                    MemberExpression outerMember = m;
                    PropertyInfo outerProp = (PropertyInfo)outerMember.Member;
                    MemberExpression innerMember = (MemberExpression)outerMember.Expression;
                    FieldInfo innerField = (FieldInfo)innerMember.Member;
                    ConstantExpression ce = (ConstantExpression)innerMember.Expression;
                    object innerObj = ce.Value;
                    object outerObj = innerField.GetValue(innerObj);
                    object value = outerProp.GetValue(outerObj, null);
                    this.Append(value);
                    return m;

                }
            }
            throw new NotSupportedException(string.Format("成员{0}不支持", m.Member.Name));
            //return base.VisitMember(m);
        }

    }

    public partial class WhereGenerateVisitor : IWhereGnerate
    {

        public string Explain(Expression expression)
        {

            head = node = null;

            Visit(expression);

            if (head == null) return null;

            return $"WHERE {Analysis(head)}";

        }

        private LinkNode<string, ExpressionType?> head, node;

        private void SetOptType(ExpressionType type)
        {
            if (head == null) head = node = new LinkNode<string, ExpressionType?>(type);
            else if (node.Flag == null) node.Flag = type;
            else
            {
                if (node.Flag == ExpressionType.Not)
                {
                    if (type == ExpressionType.Equal)
                        node.Flag = ExpressionType.NotEqual;
                    else if (type == ExpressionType.NotEqual)
                        node.Flag = ExpressionType.Equal;
                    else if (type == ExpressionType.LessThan)
                        node.Flag = ExpressionType.GreaterThanOrEqual;
                    else if (type == ExpressionType.GreaterThan)
                        node.Flag = ExpressionType.LessThanOrEqual;
                    else if (type == ExpressionType.GreaterThanOrEqual)
                        node.Flag = ExpressionType.LessThan;
                    else if (type == ExpressionType.LessThanOrEqual)
                        node.Flag = ExpressionType.GreaterThan;
                    else throw new NotSupportedException(string.Format("运算{0}不支持 NOT ", type));
                }
            }
        }

        private void Append(string str)
        {
            if (head == null) head = node = new LinkNode<string, ExpressionType?>(str);
            else if (node.Val == null) node.Val = str;
            else node = node.Next = new LinkNode<string, ExpressionType?>(str);
        }

        private void Append(object value)
        {
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Boolean:
                    this.Append((((bool)value) ? 1 : 0).ToString());
                    break;
                case TypeCode.String:
                    this.Append($"'{value}'");
                    break;
                case TypeCode.DateTime:
                    this.Append($"'{(DateTime)value:yyyy-MM-dd hh:mm:ss ffff}'");
                    break;
                case TypeCode.Object:
                    throw new NotSupportedException(string.Format("常量{0}不支持", value));
                default:
                    this.Append(value.ToString());
                    break;
            }
        }

        private string Analysis(LinkNode<string, ExpressionType?> node)
        {

            if (node.Next == null) return node.Val;

            string right = node.Next.ToString();

            switch (node.Flag)
            {
                case ExpressionType.AndAlso:
                case ExpressionType.And:
                    return $"{node.Val} AND {right}";
                case ExpressionType.Or:
                    return $"({node.Val} OR {right})";
                case ExpressionType.Equal:
                    if (right == null)
                        return $"{node.Val} IS NULL";
                    else
                        return $"{node.Val} = {right}";
                case ExpressionType.NotEqual:
                    if (right == null)
                        return $"{node.Val} IS NOT NULL";
                    else
                        return $"{node.Val} <> {right}";
                case ExpressionType.LessThan:
                    return $"{node.Val} < {right}";
                case ExpressionType.LessThanOrEqual:
                    return $"{node.Val} <= {right}";
                case ExpressionType.GreaterThan:
                    return $"{node.Val} > {right}";
                case ExpressionType.GreaterThanOrEqual:
                    return $"{node.Val} >= {right}";
                case ExpressionType.Add:
                    return $"({node.Val} + {right})";
                default:
                    throw new NotSupportedException(string.Format("运算符{0}不支持", node.Flag));
            }
        }

    }

}
