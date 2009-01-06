﻿using System.Text;
using System.Data.Common.CommandTrees;
using System.Data.Metadata.Edm;

namespace MySql.Data.MySqlClient.Generator
{
    class InsertGenerator : SqlGenerator 
    {
        private StringBuilder sql;

        public override string GenerateSQL(DbCommandTree tree)
        {
            DbInsertCommandTree commandTree = tree as DbInsertCommandTree;

       //     StringBuilder commandText = new StringBuilder(s_commandTextBuilderInitialCapacity);
            //ExpressionTranslator translator = new ExpressionTranslator(commandText, tree,
                //null != tree.Returning);

            sql = new StringBuilder();
            sql.Append("INSERT  ");

            current = sql;
            commandTree.Target.Expression.Accept(this);
            
            sql.Append("(");
            foreach (DbSetClause setClause in commandTree.SetClauses)
            {
                setClause.Property.Accept(this);
                sql.Append(",");
            }
            sql[sql.Length-1] = ')';

            // values c1, c2, ...
            //first = true;
            sql.Append(" VALUES (");
            foreach (DbSetClause setClause in commandTree.SetClauses)
            {
                setClause.Value.Accept(this);
                sql.Append(",");

                //translator.RegisterMemberValue(setClause.Property, setClause.Value);
            }
            sql[sql.Length - 1] = ')';

            // generate returning sql
/*            GenerateReturningSql(commandText, tree, translator, tree.Returning); 

            parameters = translator.Parameters;
            return commandText.ToString();*/
            return sql.ToString();
        }

        public override void Visit(DbPropertyExpression expression)
        {
            current.Append(QuoteIdentifier(expression.Property.Name));
        }

        public override void Visit(DbConstantExpression expression)
        {
            // create a parameter and save it for later when we are 
            // making our command that will be returned.
            MySqlParameter p = new MySqlParameter();
            p.ParameterName = CreateUniqueParameterName();
            p.DbType = GetDbType(expression.ResultType);
            p.Value = expression.Value;
            Parameters.Add(p);

            // now add the parameter name to our SQL stream
            current.Append(p.ParameterName);
        }
    }
}