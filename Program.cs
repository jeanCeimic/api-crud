// Program.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios al contenedor
builder.Services.AddControllers();

// Configurar Entity Framework con PostgreSQL
builder.Services.AddDbContext<ProductosDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(30);
    });
    options.EnableDetailedErrors();
    options.EnableSensitiveDataLogging();
});

// Configurar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configurar el pipeline de HTTP
app.UseCors("AllowAngular");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Aplicar migraciones automáticamente al iniciar
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ProductosDbContext>();
    try
    {
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        // Log error pero no fallar la aplicación
        Console.WriteLine($"Error aplicando migraciones: {ex.Message}");
    }
}

app.Run();

// Modelo de datos con anotaciones para Entity Framework
[Table("productos")]
public class Producto
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Column("descripcion")]
    [MaxLength(500)]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    [Column("precio", TypeName = "decimal(10,2)")]
    public decimal Precio { get; set; }

    [Column("fecha_creacion")]
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    [Column("activo")]
    public bool Activo { get; set; } = true;
}

// DbContext
public class ProductosDbContext : DbContext
{
    public ProductosDbContext(DbContextOptions<ProductosDbContext> options) : base(options) { }

    public DbSet<Producto> Productos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración adicional del modelo
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasIndex(p => p.Nombre).HasDatabaseName("idx_productos_nombre");
            entity.Property(p => p.FechaCreacion).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Datos iniciales (seed data)
        modelBuilder.Entity<Producto>().HasData(
            new Producto 
            { 
                Id = 1, 
                Nombre = "Laptop HP", 
                Descripcion = "Laptop para trabajo y estudio", 
                Precio = 1200.00m,
                FechaCreacion = DateTime.UtcNow
            },
            new Producto 
            { 
                Id = 2, 
                Nombre = "Mouse Logitech", 
                Descripcion = "Mouse inalámbrico ergonómico", 
                Precio = 25.50m,
                FechaCreacion = DateTime.UtcNow
            },
            new Producto 
            { 
                Id = 3, 
                Nombre = "Teclado Mecánico", 
                Descripcion = "Teclado mecánico RGB", 
                Precio = 75.00m,
                FechaCreacion = DateTime.UtcNow
            }
        );
    }
}

// DTOs para requests/responses
public class ProductoCreateDto
{
    [Required]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")]
    public decimal Precio { get; set; }
}

public class ProductoUpdateDto
{
    [Required]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")]
    public decimal Precio { get; set; }

    public bool Activo { get; set; } = true;
}

// Controlador actualizado con Entity Framework
[ApiController]
[Route("api/[controller]")]
public class ProductosController : ControllerBase
{
    private readonly ProductosDbContext _context;

    public ProductosController(ProductosDbContext context)
    {
        _context = context;
    }

    // GET: api/productos
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Producto>>> GetProductos()
    {
        var productos = await _context.Productos
            .Where(p => p.Activo)
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync();

        return Ok(productos);
    }

    // GET: api/productos/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Producto>> GetProducto(int id)
    {
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo);
        
        if (producto == null)
        {
            return NotFound(new { mensaje = "Producto no encontrado" });
        }
        
        return Ok(producto);
    }

    // POST: api/productos
    [HttpPost]
    public async Task<ActionResult<Producto>> CreateProducto(ProductoCreateDto productoDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var producto = new Producto
        {
            Nombre = productoDto.Nombre,
            Descripcion = productoDto.Descripcion,
            Precio = productoDto.Precio,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, producto);
    }

    // PUT: api/productos/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProducto(int id, ProductoUpdateDto productoDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var productoExistente = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo);
        
        if (productoExistente == null)
        {
            return NotFound(new { mensaje = "Producto no encontrado" });
        }

        productoExistente.Nombre = productoDto.Nombre;
        productoExistente.Descripcion = productoDto.Descripcion;
        productoExistente.Precio = productoDto.Precio;
        productoExistente.Activo = productoDto.Activo;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return StatusCode(409, new { mensaje = "Error de concurrencia. El producto fue modificado por otro usuario." });
        }
        
        return Ok(productoExistente);
    }

    // DELETE: api/productos/5 (Soft delete)
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProducto(int id)
    {
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo);
        
        if (producto == null)
        {
            return NotFound(new { mensaje = "Producto no encontrado" });
        }

        // Soft delete - solo marcar como inactivo
        producto.Activo = false;
        await _context.SaveChangesAsync();
        
        return Ok(new { mensaje = "Producto eliminado correctamente" });
    }

    // DELETE: api/productos/5/permanente (Hard delete)
    [HttpDelete("{id}/permanente")]
    public async Task<IActionResult> DeleteProductoPermanente(int id)
    {
        var producto = await _context.Productos.FindAsync(id);
        
        if (producto == null)
        {
            return NotFound(new { mensaje = "Producto no encontrado" });
        }

        _context.Productos.Remove(producto);
        await _context.SaveChangesAsync();
        
        return Ok(new { mensaje = "Producto eliminado permanentemente" });
    }

    // GET: api/productos/buscar?nombre=laptop&precioMin=100&precioMax=1000
    [HttpGet("buscar")]
    public async Task<ActionResult<IEnumerable<Producto>>> BuscarProductos(
        [FromQuery] string? nombre = null,
        [FromQuery] decimal? precioMin = null,
        [FromQuery] decimal? precioMax = null,
        [FromQuery] bool incluirInactivos = false)
    {
        var query = _context.Productos.AsQueryable();

        if (!incluirInactivos)
        {
            query = query.Where(p => p.Activo);
        }

        if (!string.IsNullOrEmpty(nombre))
        {
            query = query.Where(p => p.Nombre.ToLower().Contains(nombre.ToLower()) ||
                                   p.Descripcion.ToLower().Contains(nombre.ToLower()));
        }

        if (precioMin.HasValue)
        {
            query = query.Where(p => p.Precio >= precioMin.Value);
        }

        if (precioMax.HasValue)
        {
            query = query.Where(p => p.Precio <= precioMax.Value);
        }

        var resultados = await query
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync();

        return Ok(resultados);
    }

    // Clase para mapear los resultados de la vista de estadísticas
    private class EstadisticasProductos
    {
        public int total_productos { get; set; }
        public int productos_activos { get; set; }
        public int productos_inactivos { get; set; }
        public decimal precio_promedio { get; set; }
        public decimal precio_minimo { get; set; }
        public decimal precio_maximo { get; set; }
        public string? producto_mas_caro { get; set; }
        public string? producto_mas_barato { get; set; }
    }

    // GET: api/productos/estadisticas
    [HttpGet("estadisticas")]
    public async Task<ActionResult> GetEstadisticas()
    {
        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            // Consulta optimizada que evita subconsultas y usa window functions
            command.CommandText = @"
                SELECT 
                    activos.total_activos as productos_activos,
                    COALESCE(inactivos.total_inactivos, 0) as productos_inactivos,
                    activos.precio_promedio,
                    activos.precio_minimo,
                    activos.precio_maximo,
                    CASE 
                        WHEN activos.total_activos > 0 THEN 
                            jsonb_build_object('nombre', activos.nombre_mas_caro, 'precio', activos.precio_maximo)
                        ELSE NULL 
                    END as producto_mas_caro,
                    CASE 
                        WHEN activos.total_activos > 0 THEN 
                            jsonb_build_object('nombre', activos.nombre_mas_barato, 'precio', activos.precio_minimo)
                        ELSE NULL 
                    END as producto_mas_barato
                FROM (
                    SELECT 
                        COUNT(*) as total_activos,
                        ROUND(AVG(precio), 2) as precio_promedio,
                        MIN(precio) as precio_minimo,
                        MAX(precio) as precio_maximo,
                        (array_agg(nombre ORDER BY precio DESC))[1] as nombre_mas_caro,
                        (array_agg(nombre ORDER BY precio))[1] as nombre_mas_barato
                    FROM productos 
                    WHERE activo = true
                ) activos
                CROSS JOIN (
                    SELECT COUNT(*) as total_inactivos
                    FROM productos 
                    WHERE activo = false
                ) inactivos";

            command.CommandTimeout = 15; // Reducimos el timeout ya que la consulta es más rápida

            await _context.Database.OpenConnectionAsync();
            
            try
            {
                using var result = await command.ExecuteReaderAsync();
                if (await result.ReadAsync())
                {
                    var productosActivos = result.GetInt64(0);
                    var productosInactivos = result.GetInt64(1);

                    return Ok(new
                    {
                        TotalProductos = productosActivos + productosInactivos,
                        ProductosActivos = productosActivos,
                        ProductosInactivos = productosInactivos,
                        PrecioPromedio = result.IsDBNull(2) ? 0m : result.GetDecimal(2),
                        PrecioMinimo = result.IsDBNull(3) ? 0m : result.GetDecimal(3),
                        PrecioMaximo = result.IsDBNull(4) ? 0m : result.GetDecimal(4),
                        ProductoMasCaro = result.IsDBNull(5) ? new { Nombre = (string?)null, Precio = 0m }
                            : System.Text.Json.JsonSerializer.Deserialize<dynamic>(result.GetString(5)),
                        ProductoMasBarato = result.IsDBNull(6) ? new { Nombre = (string?)null, Precio = 0m }
                            : System.Text.Json.JsonSerializer.Deserialize<dynamic>(result.GetString(6))
                    });
                }

                return Ok(new
                {
                    TotalProductos = 0L,
                    ProductosActivos = 0L,
                    ProductosInactivos = 0L,
                    PrecioPromedio = 0m,
                    PrecioMinimo = 0m,
                    PrecioMaximo = 0m,
                    ProductoMasCaro = new { Nombre = (string?)null, Precio = 0m },
                    ProductoMasBarato = new { Nombre = (string?)null, Precio = 0m }
                });
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                mensaje = "Error al obtener las estadísticas",
                error = ex.Message,
                detalles = ex.InnerException?.Message 
            });
        }
    }

    // GET: api/productos/inactivos
    [HttpGet("inactivos")]
    public async Task<ActionResult<IEnumerable<Producto>>> GetProductosInactivos()
    {
        var productosInactivos = await _context.Productos
            .Where(p => !p.Activo)
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync();

        return Ok(productosInactivos);
    }

    // PUT: api/productos/5/restaurar
    [HttpPut("{id}/restaurar")]
    public async Task<IActionResult> RestaurarProducto(int id)
    {
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && !p.Activo);
        
        if (producto == null)
        {
            return NotFound(new { mensaje = "Producto inactivo no encontrado" });
        }

        producto.Activo = true;
        await _context.SaveChangesAsync();
        
        return Ok(new { mensaje = "Producto restaurado correctamente", producto });
    }
}