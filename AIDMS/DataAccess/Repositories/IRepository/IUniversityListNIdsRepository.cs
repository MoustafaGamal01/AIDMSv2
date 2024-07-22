using AIDMS.Entities;
using System.Threading.Tasks;

namespace AIDMS.Repositories
{
    public interface IUniversityListNIdsRepository
    {
        Task <UniversityListNationaIds> CheckExistanceOfNationalId(string NationalId);
    }
}
