using Microsoft.AspNetCore.Mvc;

namespace tetsapi.Controllers;

[Route("api/[controller]")]
[ApiController]

public class ValController : ControllerBase

{

    // GET api/values

    [HttpGet]

    public ActionResult<IEnumerable<string>> Get()

    {

        return new string[] { "value1", "value2" };

    }

 

    // GET api/values/5

    [HttpGet("{id}")]

    public ActionResult<string> Get(int id)

    {

        return "Welcome to AspSolution.net" + id;

    }

 

    // POST api/values

    [HttpPost]

    public void Post([FromBody] string value)

    {

    }

 

    // PUT api/values/5

    [HttpPut("{id}")]

    public void Put(int id, [FromBody] string value)

    {

    }

 

    // DELETE api/values/5

    [HttpDelete("{id}")]

    public void Delete(int id)

    {

    }

}