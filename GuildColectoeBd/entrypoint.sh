#!/bin/sh

# Verifica se o diretório de montagem já está montado
if mountpoint -q /mnt/palguard; then
    echo "Desmontando a unidade de rede existente..."
    umount /mnt/palguard
fi

# Monta a unidade de rede
echo "Montando a unidade de rede..."
mount -t cifs -o username=felli,password=Unreal05 //192.168.100.73/palguard /mnt/palguard

# Executa o aplicativo
exec dotnet GuildColectoeBd.dll
