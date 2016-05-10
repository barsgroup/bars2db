﻿using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Bars2Db.Extensions;

namespace Bars2Db.Mapping
{
    public class EntityMappingBuilder<T>
    {
        public EntityMappingBuilder<TE> Entity<TE>(string configuration = null)
        {
            return _builder.Entity<TE>(configuration);
        }

        public PropertyMappingBuilder<T> Property(Expression<Func<T, object>> func)
        {
            return new PropertyMappingBuilder<T>(this, func);
        }

        public EntityMappingBuilder<T> HasPrimaryKey(Expression<Func<T, object>> func, int order = -1)
        {
            var n = 0;

            return SetAttribute(
                func,
                true,
                m => new PrimaryKeyAttribute(Configuration, order + n++ + (m && order == -1 ? 1 : 0)),
                (m, a) => a.Order = order + n++ + (m && order == -1 ? 1 : 0),
                a => a.Configuration);
        }

        public EntityMappingBuilder<T> HasIdentity(Expression<Func<T, object>> func)
        {
            return SetAttribute(
                func,
                false,
                _ => new IdentityAttribute(Configuration),
                (_, a) => { },
                a => a.Configuration);
        }

        public EntityMappingBuilder<T> HasColumn(Expression<Func<T, object>> func, int order = -1)
        {
            return SetAttribute(
                func,
                true,
                _ => new ColumnAttribute(Configuration),
                (_, a) => a.IsColumn = true,
                a => a.Configuration);
        }

        public EntityMappingBuilder<T> Ignore(Expression<Func<T, object>> func, int order = -1)
        {
            return SetAttribute(
                func,
                true,
                _ => new NotColumnAttribute {Configuration = Configuration},
                (_, a) => a.IsColumn = false,
                a => a.Configuration);
        }

        public EntityMappingBuilder<T> HasTableName(string tableName)
        {
            return SetTable(a => a.Name = tableName);
        }

        public EntityMappingBuilder<T> HasSchemaName(string schemaName)
        {
            return SetTable(a => a.Schema = schemaName);
        }

        public EntityMappingBuilder<T> HasDatabaseName(string databaseName)
        {
            return SetTable(a => a.Database = databaseName);
        }

        private EntityMappingBuilder<T> SetTable(Action<TableAttribute> setColumn)
        {
            return SetAttribute(
                () =>
                {
                    var a = new TableAttribute {Configuration = Configuration, IsColumnAttributeRequired = false};
                    setColumn(a);
                    return a;
                },
                setColumn,
                a => a.Configuration,
                a => new TableAttribute
                {
                    Configuration = a.Configuration,
                    Name = a.Name,
                    Schema = a.Schema,
                    Database = a.Database,
                    IsColumnAttributeRequired = a.IsColumnAttributeRequired
                });
        }

        private EntityMappingBuilder<T> SetAttribute<TA>(
            Func<TA> getNew,
            Action<TA> modifyExisting,
            Func<TA, string> configGetter,
            Func<TA, TA> overrideAttribute)
            where TA : Attribute
        {
            var attrs = GetAttributes(typeof(T), configGetter);

            if (attrs.Length == 0)
            {
                var attr = _builder.MappingSchema.GetAttribute(typeof(T), configGetter);

                if (attr != null)
                {
                    var na = overrideAttribute(attr);

                    modifyExisting(na);
                    _builder.HasAttribute(typeof(T), na);

                    return this;
                }

                _builder.HasAttribute(typeof(T), getNew());
            }
            else
                modifyExisting(attrs[0]);

            return this;
        }

        internal EntityMappingBuilder<T> SetAttribute<TA>(
            Expression<Func<T, object>> func,
            bool processNewExpression,
            Func<bool, TA> getNew,
            Action<bool, TA> modifyExisting,
            Func<TA, string> configGetter,
            Func<TA, TA> overrideAttribute = null)
            where TA : Attribute
        {
            var ex = func.Body;

            if (ex is UnaryExpression)
                ex = ((UnaryExpression) ex).Operand;

            Action<Expression, bool> setAttr = (e, m) =>
            {
                var memberExpression = e as MemberExpression;
                var memberInfo =
                    memberExpression?.Member ?? (e as MethodCallExpression)?.Method;

                if (e is MemberExpression && memberInfo.ReflectedTypeEx() != typeof(T))
                    memberInfo = typeof(T).GetPropertyEx(memberInfo.Name);

                if (memberInfo == null)
                    throw new ArgumentException(string.Format("'{0}' cant be converted to a class member.", e));

                var attrs = GetAttributes(memberInfo, configGetter);

                if (attrs.Length == 0)
                {
                    if (overrideAttribute != null)
                    {
                        var attr = _builder.MappingSchema.GetAttribute(memberInfo, configGetter);

                        if (attr != null)
                        {
                            var na = overrideAttribute(attr);

                            modifyExisting(m, na);
                            _builder.HasAttribute(memberInfo, na);

                            return;
                        }
                    }

                    _builder.HasAttribute(memberInfo, getNew(m));
                }
                else
                    modifyExisting(m, attrs[0]);
            };

            if (processNewExpression && ex.NodeType == ExpressionType.New)
            {
                var nex = (NewExpression) ex;

                if (nex.Arguments.Count > 0)
                {
                    foreach (var arg in nex.Arguments)
                        setAttr(arg, true);
                    return this;
                }
            }

            setAttr(ex, false);

            return this;
        }

        #region Init

        public EntityMappingBuilder([Properties.NotNull] FluentMappingBuilder builder, string configuration)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            _builder = builder;
            Configuration = configuration;
        }

        private readonly FluentMappingBuilder _builder;

        public string Configuration { get; }

        #endregion

        #region GetAttributes

        public TA[] GetAttributes<TA>()
            where TA : Attribute
        {
            return _builder.GetAttributes<TA>(typeof(T));
        }

        public TA[] GetAttributes<TA>(Type type)
            where TA : Attribute
        {
            return _builder.GetAttributes<TA>(type);
        }

        public TA[] GetAttributes<TA>(MemberInfo memberInfo)
            where TA : Attribute
        {
            return _builder.GetAttributes<TA>(memberInfo);
        }

        public TA[] GetAttributes<TA>(Func<TA, string> configGetter)
            where TA : Attribute
        {
            var attrs = GetAttributes<TA>();

            return string.IsNullOrEmpty(Configuration)
                ? attrs.Where(a => string.IsNullOrEmpty(configGetter(a))).ToArray()
                : attrs.Where(a => Configuration == configGetter(a)).ToArray();
        }

        public TA[] GetAttributes<TA>(Type type, Func<TA, string> configGetter)
            where TA : Attribute
        {
            var attrs = GetAttributes<TA>(type);

            return string.IsNullOrEmpty(Configuration)
                ? attrs.Where(a => string.IsNullOrEmpty(configGetter(a))).ToArray()
                : attrs.Where(a => Configuration == configGetter(a)).ToArray();
        }

        public TA[] GetAttributes<TA>(MemberInfo memberInfo, Func<TA, string> configGetter)
            where TA : Attribute
        {
            var attrs = GetAttributes<TA>(memberInfo);

            return string.IsNullOrEmpty(Configuration)
                ? attrs.Where(a => string.IsNullOrEmpty(configGetter(a))).ToArray()
                : attrs.Where(a => Configuration == configGetter(a)).ToArray();
        }

        #endregion

        #region HasAttribute

        public EntityMappingBuilder<T> HasAttribute(Attribute attribute)
        {
            _builder.HasAttribute(typeof(T), attribute);
            return this;
        }

        public EntityMappingBuilder<T> HasAttribute(MemberInfo memberInfo, Attribute attribute)
        {
            _builder.HasAttribute(memberInfo, attribute);
            return this;
        }

        public EntityMappingBuilder<T> HasAttribute(LambdaExpression func, Attribute attribute)
        {
            _builder.HasAttribute(func, attribute);
            return this;
        }

        public EntityMappingBuilder<T> HasAttribute(Expression<Func<T, object>> func, Attribute attribute)
        {
            _builder.HasAttribute(func, attribute);
            return this;
        }

        #endregion
    }
}