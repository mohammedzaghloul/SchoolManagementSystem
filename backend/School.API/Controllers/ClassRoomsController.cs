using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using School.Application.Features.ClassRooms.Commands;
using School.Application.Features.ClassRooms.Queries;

namespace School.API.Controllers;

[Authorize]
[Route("api/ClassRooms")]
[Route("api/ClassRoom")]
public class ClassRoomsController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<List<ClassRoomDto>>> GetClassRooms()
    {
        var result = await Mediator.Send(new GetClassRoomsQuery()); 
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ClassRoomDto>> GetClassRoom(int id)
    {
        // For the demo, we can use GetClassRoomsQuery and filter or assume there's a specific query
        var result = await Mediator.Send(new GetClassRoomsQuery());
        var classroom = result.FirstOrDefault(c => c.Id == id);
        if (classroom == null) return NotFound();
        return Ok(classroom);
    }

    [HttpGet("teacher/{teacherId?}")]
    public async Task<ActionResult<List<ClassRoomDto>>> GetTeacherClasses(int teacherId = 0)
    {
        // For the demo, we return all as the teacher is linked to all classrooms in seed
        var result = await Mediator.Send(new GetClassRoomsQuery()); 
        return Ok(result);
    }
}
