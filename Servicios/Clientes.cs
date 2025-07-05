using Microsoft.EntityFrameworkCore;
using PelqueriaLuciaEspina.Enums;
using PeluqueriaLuciaEspina.Models;
using PeluqueriaLuciaEspina.Repo;
using PeluqueriaLuciaEspina.Services;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace PeluqueriaLuciaEspina.Servicios
{
        public interface IClienteService
        {
            Task<IEnumerable<Cliente>> ObtenerTodosAsync(Sexo ? sexo);
            Task<Cliente?> ObtenerPorIdAsync(int id);
            Task CrearAsync(Cliente cliente);
            Task ActualizarAsync(Cliente cliente);
            Task EliminarAsync(int id);
            Task<bool> AnyAsync(Expression<Func<Cliente, bool>> predicate);
        }
    public class ClienteService : IClienteService
    {
        private readonly IGenericRepository<Cliente> _clienteRepo;

        public ClienteService(IGenericRepository<Cliente> clienteRepository)
        {
            _clienteRepo = clienteRepository;
        }

        public async Task<IEnumerable<Cliente>> ObtenerTodosAsync(Sexo? sexo)
        {
            var clientes = _clienteRepo.GetAll();

            if (sexo.HasValue)
                return await clientes.Where(c => c.Sexo == sexo.Value).ToListAsync();

            return await clientes.ToListAsync();
        }


        public async Task<Cliente?> ObtenerPorIdAsync(int id)
        {
            return await _clienteRepo.GetByIdAsync(id);
        }

        public async Task CrearAsync(Cliente cliente)
        {
            await _clienteRepo.AddAsync(cliente);
            await _clienteRepo.SaveChangesAsync();
        }


        public async Task ActualizarAsync(Cliente cliente)
        {
            await _clienteRepo.UpdateAsync(cliente);
            await _clienteRepo.SaveChangesAsync();
        }

        public async Task EliminarAsync(int id)
        {
            var cliente = await _clienteRepo.GetByIdAsync(id);
            if (cliente != null)
            {
                await _clienteRepo.DeleteAsync(cliente);
                await _clienteRepo.SaveChangesAsync();
            }
        }
        public async Task<bool> AnyAsync(Expression<Func<Cliente, bool>> predicate)
        {
            return await _clienteRepo.AnyAsync(predicate);
        }

    }





}
