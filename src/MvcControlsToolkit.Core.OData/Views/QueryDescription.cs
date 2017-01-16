﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using MvcControlsToolkit.Core.Linq;
using MvcControlsToolkit.Core.DataAnnotations;
using MvcControlsToolkit.Core.DataAnnotations.Queries;
using System.Text;
using System.Globalization;

namespace MvcControlsToolkit.Core.Views
{

    public abstract class QueryDescription
    {
        private const string filterName = "$filer";
        private const string applyName = "$apply";
        private const string sortingName = "$orderby";
        private const string searchName = "$search";
        private const string topName = "$top";
        private const string skipName = "$skip";
        protected static Func<string, string> UrlEncode =
            System.Net.WebUtility.UrlEncode;
        private QueryFilterBooleanOperator _Filter;
        public QueryFilterBooleanOperator Filter { get { return _Filter; } set {_Filter=value; ClearFilterCache(); } }
        private QuerySearch _Search;
        public QuerySearch Search { get { return _Search; } set { _Search = value; ClearSearchCache(); } }

        private ICollection<QuerySortingCondition> _Sorting;
        public ICollection<QuerySortingCondition> Sorting { get { return _Sorting; } set { _Sorting = value; ClearSortingCache(); } }
        private QueryGrouping _Grouping;
        public QueryGrouping Grouping { get { return _Grouping; } set { _Grouping = value; ClearGroupingCache(); } }
        public long Skip { get; set; }

        public long? Take { get; set; }

        public long Page { get; set; }

        public string EncodeSorting()
        {
            if (Sorting == null || Sorting.Count == 0) return null;
            if (Sorting.Count == 1) return Sorting.First().ToString();
            StringBuilder sb = new StringBuilder();
            foreach (var c in Sorting)
            {
                if (sb.Length > 0) sb.Append(",");
                sb.Append(c.ToString());
            }
            return sb.ToString();
        }
        public string EncodeFilter()
        {
            if (Filter == null) return null;
            return Filter.ToString();
        }
        public string EncodeSearch()
        {
            if (Search == null) return null;
            return Search.ToString();
        }
        public string EncodeGrouping()
        {
            if (Grouping == null) return null;
            return Grouping.ToString();
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            string search = EncodeSearch();
            string filter = null;
            if(search != null)
            {
                sb.Append(searchName);
                sb.Append("=");
                sb.Append(UrlEncode(search));
            }
            else
            {
                filter = EncodeFilter();
                if(filter != null)
                {
                    sb.Append(filterName);
                    sb.Append("=");
                    sb.Append(UrlEncode(filter));
                }
            }
            string apply = EncodeGrouping();
            if(apply != null)
            {
                if (sb.Length > 0) sb.Append("&");
                sb.Append(applyName);
                sb.Append("=");
                sb.Append(UrlEncode(apply));
            }
            string sorting = EncodeSorting();
            if (sorting != null)
            {
                if (sb.Length > 0) sb.Append("&");
                sb.Append(sortingName);
                sb.Append("=");
                sb.Append(UrlEncode(sorting));
            }
            if (Skip > 0)
            {
                if (sb.Length > 0) sb.Append("&");
                sb.Append(skipName);
                sb.Append("=");
                sb.Append(Skip.ToString(CultureInfo.InvariantCulture));
            }
            if (Take.HasValue)
            {
                if (sb.Length > 0) sb.Append("&");
                sb.Append(topName);
                sb.Append("=");
                sb.Append(Take.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (sb.Length > 0) return sb.ToString();
            else return null;
        }
        public string AddToUrl(string url)
        {
            if (url == null) url = string.Empty;
            var query = ToString();
            if (string.IsNullOrWhiteSpace(query)) return url;
            if (url.Contains("?")) return url + "&" + query;
            else return url + "?" + query;
        }
        public  QueryDescription Clone()
        {
            return CloneInternal();
        }

        protected abstract QueryDescription CloneInternal();
        protected abstract void ClearFilterCache();
        protected abstract void ClearSearchCache();
        protected abstract void ClearGroupingCache();
        protected abstract void ClearSortingCache();

    }


    public class QueryDescription<T>: QueryDescription
    {

        private Expression<Func<T, bool>> filterCache;
        private Func<IQueryable<T>, IOrderedQueryable<T>> sortingCache;
        private Func<IQueryable<T>, IQueryable<T>> groupingCache;
        
        public bool SearchAllowed()
        {
            if (Search == null) return false;
            return Search.Allowed(typeof(T));
        }

        public Expression<Func<T, bool>> GetFilterExpression()
        {
            if (Filter == null) return null;
            if (filterCache != null) return filterCache;
            var par = Expression.Parameter(typeof(T), "m");
            return filterCache=Expression.Lambda(Filter.BuildExpression(par, typeof(T)), par) as Expression<Func<T, bool>>;

        }
        public Func<IQueryable<T>, IQueryable<T>> GetGrouping()
        {
            if (groupingCache != null) return groupingCache;
            return groupingCache=GetGrouping<T>();
        }
        public Func<IQueryable<T>, IQueryable<F>> GetGrouping<F>()
        {
            if (Grouping == null) return null;
            var keys = Grouping.BuildGroupingExpression<T>();
            if (keys == null) return null;
            var aggregations = Grouping.GetProjectionExpression<T, F>();
            return q =>
            {
                return q.GroupBy(keys).Select(aggregations);
            };
        }
        public Func<IQueryable<T>, IOrderedQueryable<T>> GetSorting()
        {
            if (Sorting == null || Sorting.Count == 0) return sortingCache=null;
            return sortingCache = (q) =>
            {
                bool start = true;
                IOrderedQueryable<T> result = null;
                foreach (var s in Sorting)
                {
                    if (start)
                    {
                        start = false;
                        if (s.Down) result = q.OrderByDescending(s.GetSortingLambda(typeof(T)));
                        else result = q.OrderBy(s.GetSortingLambda(typeof(T)));
                    }
                    else
                    {
                        if (s.Down) result = result.ThenByDescending(s.GetSortingLambda(typeof(T)));
                        else result = result.ThenBy(s.GetSortingLambda(typeof(T)));
                    }
                }
                return result;
            };
        }
        
        protected override QueryDescription CloneInternal()
        {
            return MemberwiseClone() as QueryDescription;
        }
        protected override void ClearFilterCache()
        {
            filterCache = null;
        }
        protected override void ClearGroupingCache()
        {
            groupingCache=null;
        }
        protected override void ClearSearchCache()
        {
           
        }
        protected override void ClearSortingCache()
        {
            sortingCache = null; ;
        }
        public new QueryDescription<T> Clone()
        {
            return MemberwiseClone() as QueryDescription<T>;
        }
        
        public void AddFilterCondition(Expression<Func<T, bool>> filter, bool useOr=false)
        {
            if (filter == null) return;
            var res = QueryFilterClause.FromLinQExpression(filter);
            if (res == null) return;
            if (Filter == null) Filter = res is QueryFilterBooleanOperator ? res as QueryFilterBooleanOperator
                    : new QueryFilterBooleanOperator(res, null);

            QueryFilterClause cleanFilter;
            if (Filter.Operator != QueryFilterBooleanOperator.not)
            {
                if (Filter.Child1 == null && Filter.Argument1 == null) cleanFilter = Filter.Argument2 as QueryFilterClause ?? Filter.Child2;
                else if (Filter.Child2 == null && Filter.Argument2 == null) cleanFilter = Filter.Argument1 as QueryFilterClause ?? Filter.Child1;
                else cleanFilter = Filter;
            }
            else cleanFilter = Filter;
            Filter = new QueryFilterBooleanOperator(cleanFilter, res);
            if (useOr) Filter.Operator = QueryFilterBooleanOperator.or;    

        }

    }
}
