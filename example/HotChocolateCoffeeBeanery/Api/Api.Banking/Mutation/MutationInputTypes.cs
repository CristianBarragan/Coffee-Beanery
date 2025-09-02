using Domain.Model;
namespace Api.Banking.Mutation;

public class CustomerInputType : InputObjectType<Wrapper>
{
    protected override void Configure(IInputObjectTypeDescriptor<Wrapper> inputObjectTypeDescriptor)
    {
    }
}