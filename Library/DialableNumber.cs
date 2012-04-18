using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Org.Reddragonit.FreeSwitch.Sockets
{
    public class DialableNumber : IComparable
    {
        private static readonly Regex _reg = new Regex("^[d\\*#]+$",RegexOptions.Compiled|RegexOptions.ECMAScript);

        private string _numbers;

        public DialableNumber(string numbers)
        {
            if (!_reg.IsMatch(numbers))
                throw new Exception("Invalid dialable numbers specific in the constructor, numbers must be 0-9*#, "+numbers+" is not valid");
            _numbers = numbers;
        }

        public static bool operator == (DialableNumber x, DialableNumber y)
        {
            
            return (((object)x==null && (object)y==null) ? true : (((object)x!=null && (object)y!=null) ? x.CompareTo(y) == 0 : false));
        }

        public static bool operator !=(DialableNumber x, DialableNumber y)
        {
            return !(x == y);
        }

        public static bool operator <(DialableNumber x, DialableNumber y)
        {
            return x.CompareTo(y) < 0;
        }

        public static bool operator <=(DialableNumber x, DialableNumber y)
        {
            return x.CompareTo(y) <= 0;
        }

        public static bool operator >(DialableNumber x, DialableNumber y)
        {
            return x.CompareTo(y) > 0;
        }

        public static bool operator >=(DialableNumber x, DialableNumber y)
        {
            return x.CompareTo(y) >= 0;
        }

        public override int GetHashCode()
        {
            return _numbers.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return this.CompareTo(obj) == 0;
        }

        public override string ToString()
        {
            return _numbers;
        }

        public static explicit operator DialableNumber(string numbers)
        {
            return new DialableNumber(numbers);
        }

        #region IComparable Members

        public int CompareTo(object obj)
        {
            return _numbers.CompareTo(((DialableNumber)obj)._numbers);
        }

        #endregion
    }
}
