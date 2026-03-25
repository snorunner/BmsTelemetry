using System.Text.Json.Nodes;

public interface IJsonDataWarehouse
{
    public JsonNode ProcessIncoming(JsonArray incoming);
    public JsonNode? GetJsonData();
}
