namespace XPL.Framework.Infrastructure.Persistence
{
    public interface IModelConverter<TModel, TPersistence>
        where TPersistence : ISqlId
    {
        TModel ToModel(TPersistence persisted);
        TPersistence ToPersited(TModel model);
        void CopyChanges(TModel from, TPersistence to);
    }
}
