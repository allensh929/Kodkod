﻿using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using Kodkod.Application.Permissions.Dto;
using Kodkod.Core.Permissions;
using Kodkod.Core.Roles;
using Kodkod.Core.Users;
using Kodkod.EntityFramework;
using Kodkod.EntityFramework.Repositories;
using Kodkod.Utilities.PagedList;
using Kodkod.Utilities.PagedList.Extensions;
using Kodkod.Utilities.Extensions;
using Kodkod.Utilities.Linq.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Kodkod.Application.Permissions
{
    public class PermissionAppService : IPermissionAppService
    {
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Permission> _permissionRepository;
        private readonly IRepository<Role> _roleRepository;
        private readonly IRepository<RolePermission> _rolePermissionRepository;
        private readonly IMapper _mapper;
        private readonly KodkodDbContext _context;

        public PermissionAppService(
            IRepository<User> userRepository,
            IRepository<Permission> permissionRepository,
            IRepository<Role> roleRepository,
            IRepository<RolePermission> rolePermissionRepository,
            IMapper mapper, 
            KodkodDbContext context)
        {
            _userRepository = userRepository;
            _permissionRepository = permissionRepository;
            _roleRepository = roleRepository;
            _rolePermissionRepository = rolePermissionRepository;
            _mapper = mapper;
            _context = context;
        }

        public async Task<IPagedList<PermissionListDto>> GetPermissionsAsync(GetPermissionsInput input)
        {
            var query = _permissionRepository.GetAll(
                    !input.Filter.IsNullOrEmpty(),
                    predicate => predicate.Name.Contains(input.Filter) ||
                                 predicate.DisplayName.Contains(input.Filter))
                .OrderBy(input.Sorting);

            var permissionsCount = await query.CountAsync();
            var permissions = query.PagedBy(input.PageSize, input.PageIndex).ToList();
            var permissionListDtos = _mapper.Map<List<PermissionListDto>>(permissions);

            return permissionListDtos.ToPagedList(permissionsCount);
        }

        public async Task<bool> IsPermissionGrantedForUserAsync(ClaimsPrincipal contextUser, Permission permission)
        {
            var user = await _userRepository.GetFirstOrDefaultAsync(u => u.UserName == contextUser.Identity.Name);
            if (user == null)
            {
                return false;
            }

            var grantedPermissions = user.UserRoles
                .Select(ur => ur.Role)
                .SelectMany(r => r.RolePermissions)
                .Select(rp => rp.Permission);

            return grantedPermissions.Any(p => p.Name == permission.Name);
        }

        public async Task<bool> IsPermissionGrantedForRoleAsync(Role role, Permission permission)
        {
            var existingRole = await _roleRepository.GetFirstOrDefaultAsync(r => r.Id == role.Id);
            if (existingRole == null)
            {
                return false;
            }

            var grantedPermissions = existingRole.RolePermissions
                .Select(rp => rp.Permission);

            return grantedPermissions.Any(p => p.Name == permission.Name);
        }

        public void InitializePermissions(List<Permission> permissions)
        {
            _rolePermissionRepository.Delete(_rolePermissionRepository.GetAll(true, rp => rp.Role.Name == RoleConsts.AdminRoleName));
            _context.SaveChanges();

            _permissionRepository.Delete(_permissionRepository.GetAll());
            _context.SaveChanges();

            foreach (var permission in permissions)
            {
                _permissionRepository.Insert(permission);

                var role = _roleRepository.GetFirstOrDefault(r => r.Name == RoleConsts.AdminRoleName);
                var rolePermission = new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id
                };
                _rolePermissionRepository.Insert(rolePermission);
            }

            _context.SaveChanges();
        }
    }
}