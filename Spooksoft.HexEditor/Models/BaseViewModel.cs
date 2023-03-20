using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Spooksoft.HexEditor.Models
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        private PropertyInfo GetPropertyInfo<T>(Expression<Func<T>> expression)
        {
            MemberExpression memberExpression = expression.Body as MemberExpression ?? throw new ArgumentException("Expression is not member expression!");
            PropertyInfo propInfo = memberExpression.Member as PropertyInfo ?? throw new ArgumentException("Expression doesn't point to property!");
            return propInfo;
        }

        protected void OnPropertyChanged<T>(Expression<Func<T>> property)
        {
            var propInfo = GetPropertyInfo(property);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propInfo.Name));
        }

        protected void InternalSet<T>(ref T field, Expression<Func<T>> property, T value, bool force = false)
        {
            if (!Equals(field, value) || force)
            {
                field = value;
                OnPropertyChanged(property);
            }
        }

        protected void Set<T>(ref T field, Expression<Func<T>> property, T value, bool force = false)
        {
            if (!Equals(field, value) || force)
            {
                field = value;
                OnPropertyChanged(property);
            }
        }

        protected void Set<T>(ref T field, Expression<Func<T>> property, T value, Action changeHandler, bool force = false)
        {
            if (!Equals(field, value) || force)
            {
                field = value;
                OnPropertyChanged(property);
                changeHandler();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
