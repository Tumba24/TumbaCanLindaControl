using System;
using System.Collections.Generic;

namespace Tumba.CanLindaControl.Model
{
    public abstract class BaseIni
    {
        protected Dictionary<string, string> m_keyValues;

        public virtual void AddValueError(List<string> errors, string key, Type type)
        {
            errors.Add(CreateValueError(key, type));
        }

        public virtual string CreateValueError(string key, Type type)
        {
            return string.Format("Invalid '{0}'!  Please specify a valid {1}", key, type.Name);
        }

        public virtual string GetValue(string key)
        {
            if (!m_keyValues.ContainsKey(key))
            {
                return null;
            }

            return m_keyValues[key];
        }

        public virtual string ParseStringValue(string key)
        {
            return GetValue(key);
        }

        public virtual int ParseInt32Value(string key)
        {
            string valueStr = GetValue(key);
            if (valueStr == null)
            {
                return Int32.MinValue;
            }

            int value;
            if (Int32.TryParse(valueStr, out value))
            {
                return value;
            }

            return Int32.MinValue;
        }

        public virtual void SetValues(Dictionary<string, string> keyValues)
        {
            m_keyValues = keyValues;
        }

        public virtual bool ValidateIni(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }

        public virtual bool ValidateInt32Value(int value)
        {
            return value != Int32.MinValue;
        }

        public virtual bool ValidateStrValue(string value)
        {
            return !string.IsNullOrEmpty(value);
        }
    }
}