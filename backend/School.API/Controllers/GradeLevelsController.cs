using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using School.Application.Features.GradeLevels.Commands;
using School.Application.Features.GradeLevels.Queries;

namespace School.API.Controllers;

[Authorize]
public class GradeLevelsController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<List<GradeLevelDto>>> GetGradeLevels()
    {
        var result = await Mediator.Send(new GetGradeLevelsQuery());
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GradeLevelDto>> GetGradeLevel(int id)
    {
        var result = await Mediator.Send(new GetGradeLevelByIdQuery { Id = id });
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<int>> CreateGradeLevel(CreateGradeLevelCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<bool>> UpdateGradeLevel(int id, UpdateGradeLevelCommand command)
    {
        if (id != command.Id) return BadRequest();
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<bool>> DeleteGradeLevel(int id)
    {
        var result = await Mediator.Send(new DeleteGradeLevelCommand { Id = id });
        return Ok(result);
    }
}
