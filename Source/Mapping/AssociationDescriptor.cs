﻿using System;
using System.Reflection;
using Bars2Db.Common;
using JNotNull = Bars2Db.Properties.NotNullAttribute;

namespace Bars2Db.Mapping
{
    public class AssociationDescriptor
    {
        public AssociationDescriptor(
            [JNotNull] Type type,
            [JNotNull] MemberInfo memberInfo,
            [JNotNull] string[] thisKey,
            [JNotNull] string[] otherKey,
            string storage,
            bool canBeNull)
        {
            if (memberInfo == null) throw new ArgumentNullException(nameof(memberInfo));
            if (thisKey == null) throw new ArgumentNullException(nameof(thisKey));
            if (otherKey == null) throw new ArgumentNullException(nameof(otherKey));

            if (thisKey.Length == 0)
                throw new ArgumentOutOfRangeException(
                    nameof(thisKey),
                    string.Format("Association '{0}.{1}' does not define keys.", type.Name, memberInfo.Name));

            if (thisKey.Length != otherKey.Length)
                throw new ArgumentException(
                    string.Format(
                        "Association '{0}.{1}' has different number of keys for parent and child objects.",
                        type.Name, memberInfo.Name));

            MemberInfo = memberInfo;
            ThisKey = thisKey;
            OtherKey = otherKey;
            Storage = storage;
            CanBeNull = canBeNull;
        }

        public MemberInfo MemberInfo { get; set; }
        public string[] ThisKey { get; set; }
        public string[] OtherKey { get; set; }
        public string Storage { get; set; }
        public bool CanBeNull { get; set; }

        public static string[] ParseKeys(string keys)
        {
            return keys == null ? Array<string>.Empty : keys.Replace(" ", "").Split(',');
        }
    }
}