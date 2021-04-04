﻿using Raje.DL.DB.Base;
using Raje.DL.Services.DAL.DataAccess;
using Raje.DL.Services.DAL.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Raje.DAL.EF.Base
{
    public class EFCustomQuery<T> : ICustomQuery<T>
        where T : EntityBase, new()
    {
        private readonly DbContext _dbContext;

        private List<string> _includesString;
        private List<Expression<Func<T, IEntity>>> __includesExpression;

        private List<Expression<Func<T, bool>>> _whereExpression;

        private List<LambdaExpression> _orderByAscExpression;
        private List<LambdaExpression> _orderByDescExpression;

        public EFCustomQuery(DbContext db)
        {
            _dbContext = db;
        }

        public ICustomQueryExecuter<T> All()
        {
            return this;
        }

        public ICustomQueryExecuter<T> Where(Expression<Func<T, bool>> filterExpression)
        {
            if (_whereExpression == null)
                _whereExpression = new List<Expression<Func<T, bool>>>();


            _whereExpression.Add(filterExpression);
            return this;
        }

        public ICustomQueryExecuter<T> IncludeEntity(string path)
        {
            if (_includesString == null)
                _includesString = new List<string>();

            _includesString.Add(path);

            return this;
        }

        public ICustomQueryExecuter<T> IncludeEntity(Expression<Func<T, IEntity>> path)
        {
            if (__includesExpression == null)
                __includesExpression = new List<Expression<Func<T, IEntity>>>();

            __includesExpression.Add(path);

            return this;
        }

        public ICustomQueryExecuter<T> OrderByAsc<TProp>(Expression<Func<T, TProp>> orderByAsc)
        {
            if (_orderByAscExpression == null)
                _orderByAscExpression = new List<LambdaExpression>();

            _orderByAscExpression.Add(orderByAsc);
            return this;
        }

        public ICustomQueryExecuter<T> OrderByDesc<TProp>(Expression<Func<T, TProp>> orderByDesc)
        {
            if (_orderByDescExpression == null)
                _orderByDescExpression = new List<LambdaExpression>();

            _orderByDescExpression.Add(orderByDesc);
            return this;
        }

        #region Execute

        public int Count() => CountQuery().Count();

        public async Task<int> CountAsync() => await CountQuery().CountAsync();

        public IEnumerable<T> Search() => SearchQuery().ToList();

        public async Task<IEnumerable<T>> SearchAsync() => await SearchQuery().ToListAsync();

        public async Task<T> FirstOrDefaultAsync() => await SearchQuery().FirstOrDefaultAsync();

        public IPageEntity<T> SmartPagedSearch(int pageIndex, int pageSize)
            => PageEntity(pageIndex, pageSize, SmartSearchQuery(pageIndex, pageSize).ToList());

        public async Task<IPageEntity<T>> SmartPagedSearchAsync(int pageIndex, int pageSize)
            => PageEntity(pageIndex, pageSize, await SmartSearchQuery(pageIndex, pageSize).ToListAsync());

        public IPageEntity<T> PagedSearch(int pageIndex, int pageSize)
        {
            int count = CountQuery().Count();
            var result = SearchQuery(pageIndex, pageSize).ToList();

            return PageEntity(pageIndex, pageSize, result, count);
        }

        public async Task<IPageEntity<T>> PagedSearchAsync(int pageIndex, int pageSize)
        {
            pageSize = pageSize > 0 ? pageSize : 1;
            var countTask = await CountQuery().CountAsync();

            if (countTask > 0)
            {
                var resultTask = await SearchQuery(pageIndex, pageSize).ToListAsync();
                return new PageEntity<T>(pageIndex, pageSize, resultTask, countTask);
            }
            else
                return new PageEntity<T>(pageIndex, pageSize, new List<T>(), countTask);

            //TOO: implement parallel queries (need to open more context)
        }

        private EFPageEntity<T> PageEntity(int pageIndex, int pageSize, List<T> result, int? count = null)
        {
            var page = new EFPageEntity<T>
            {
                PageIndex = pageIndex,
                PageSize = pageSize
            };

            int length = result.Count();

            if (!count.HasValue)
            {
                if (length == pageSize + 1)
                {
                    page.Data = result.Take(pageSize);
                    page.LastPage = false;
                }
                else
                {
                    page.Data = result;
                    page.LastPage = true;
                }
            }
            else
            {
                page.Data = result;
                page.TotalItens = count;
                page.TotalPages = pageSize == 0 ? pageSize : (int)Math.Ceiling((double)count.Value / pageSize);
                page.LastPage = pageIndex + 1 == page.TotalPages;
            }

            return page;
        }

        #endregion Execute

        #region configs

        protected IQueryable<T> IncludesConfig(IQueryable<T> query)
        {
            if (_includesString != null && _includesString.Any())
            {
                foreach (string str in _includesString)
                    query = query.Include(str);
            }

            if (__includesExpression != null && __includesExpression.Any())
            {
                foreach (var exp in __includesExpression)
                    query = query.Include(exp);
            }
            return query;
        }

        protected IQueryable<T> FilterConfig(IQueryable<T> query)
        {
            if (_whereExpression != null)
            {
                foreach (var wExp in _whereExpression)
                    query = query.Where(wExp);
            }

            return query;
        }

        protected IQueryable<T> OrderByConfig(IQueryable<T> query)
        {
            if (_orderByAscExpression != null && _orderByAscExpression.Any())
            {
                foreach (var orderByItem in _orderByAscExpression)
                    query = Queryable.OrderBy(query, (dynamic)orderByItem);
            }

            if (_orderByDescExpression != null && _orderByDescExpression.Any())
            {
                foreach (var orderByItem in _orderByDescExpression)
                    query = Queryable.OrderByDescending(query, (dynamic)orderByItem);
            }
            return query;
        }

        #endregion configs

        #region queries

        private IQueryable<T> Query()
        {
            return _dbContext.Set<T>().AsQueryable();
        }

        private IQueryable<T> CountQuery()
        {
            return FilterConfig(Query());
        }

        private IQueryable<T> SearchQuery()
        {
            return OrderByConfig(FilterConfig(IncludesConfig(Query())));
        }

        private IQueryable<T> SmartSearchQuery(int pageIndex, int pageSize)
        {
            return SearchQuery().Skip(pageIndex * pageSize).Take(pageSize + 1);
        }

        private IQueryable<T> SearchQuery(int pageIndex, int pageSize)
        {
            return SearchQuery().Skip(pageIndex * pageSize).Take(pageSize);
        }

        public object Select(Func<object, object> p)
        {
            throw new NotImplementedException();
        }

        #endregion queries
    }
}