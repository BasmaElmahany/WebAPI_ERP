using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Data
{
    public class ProjectModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            if (context is ProjectDbContext dbContext)
            {
                return (context.GetType(), dbContext.Schema, designTime);
            }

            return (object)(context.GetType(), designTime);
        }
    }
}
